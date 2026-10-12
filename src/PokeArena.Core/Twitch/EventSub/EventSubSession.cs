using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace PokeArena.Core.Twitch.EventSub;

/// <summary>Callbacks of an <see cref="EventSubSession"/>.</summary>
public interface IEventSubHandler
{
    /// <summary>
    /// A new session is open: create the subscriptions now (Twitch closes unused sessions after 10 s). Not called
    /// after a <c>session_reconnect</c> handover — subscriptions move along with the session.
    /// </summary>
    Task OnSessionStartedAsync(string sessionId, CancellationToken ct);

    Task OnNotificationAsync(EventSubMessage.Notification notification, CancellationToken ct);

    /// <summary>A subscription was revoked. Throw to end the session.</summary>
    Task OnRevocationAsync(EventSubMessage.Revocation revocation, CancellationToken ct);
}

/// <summary>The EventSub connection was lost; a new session (with new subscriptions) may fix it.</summary>
public sealed class EventSubConnectionLostException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// One EventSub WebSocket session, including Twitch-initiated moves to a new URL (<c>session_reconnect</c>).
/// Runs until cancelled or until the connection is lost (<see cref="EventSubConnectionLostException"/>); starting
/// a new session after a loss is the caller's job, because subscriptions have to be created again.
/// </summary>
public sealed class EventSubSession(IEventSubSocketFactory sockets, TimeProvider time, ILogger<EventSubSession> log)
{
    public static readonly Uri DefaultUrl = new("wss://eventsub.wss.twitch.tv/ws");

    /// <summary>Tolerance on top of Twitch's keepalive timeout before the connection counts as dead.</summary>
    internal static readonly TimeSpan KeepaliveGrace = TimeSpan.FromSeconds(5);

    /// <summary>Wait for the welcome frame; Twitch sends it right after connecting.</summary>
    internal static readonly TimeSpan WelcomeTimeout = TimeSpan.FromSeconds(10);

    public async Task RunAsync(IEventSubHandler handler, CancellationToken ct, Uri? url = null)
    {
        var frames = Channel.CreateUnbounded<Frame>();
        var seen = new RecentIds(capacity: 200);
        var timeout = WelcomeTimeout;
        Connection? current = null;
        Connection? pending = null;
        try
        {
            current = await Connection.OpenAsync(sockets, url ?? DefaultUrl, frames.Writer, log, ct);
            var started = false;
            while (true)
            {
                var frame = await ReadAsync(frames.Reader, timeout, ct);
                if (frame.Source != current && frame.Source != pending)
                {
                    continue; // Leftovers from a connection we already closed.
                }

                if (frame.Message is null)
                {
                    if (frame.Source == pending)
                    {
                        log.LogWarning(frame.Error, "EventSub reconnect target failed, staying on the old connection");
                        await pending.DisposeAsync();
                        pending = null;
                        continue;
                    }

                    throw new EventSubConnectionLostException($"EventSub connection closed: {frame.Error?.Message}", frame.Error);
                }

                switch (frame.Message)
                {
                    case EventSubMessage.Welcome welcome when pending is not null && frame.Source == pending:
                        // Only now may the old connection go: it delivers events until the new one is welcomed.
                        log.LogInformation("EventSub session moved to a new connection");
                        var previous = current;
                        current = pending;
                        pending = null;
                        await previous.DisposeAsync();
                        timeout = welcome.KeepaliveTimeout + KeepaliveGrace;
                        break;

                    case EventSubMessage.Welcome welcome when !started:
                        started = true;
                        timeout = welcome.KeepaliveTimeout + KeepaliveGrace;
                        log.LogInformation("EventSub session {SessionId} open", welcome.SessionId);
                        await handler.OnSessionStartedAsync(welcome.SessionId, ct);
                        break;

                    case EventSubMessage.Notification notification:
                        if (seen.Add(notification.MessageId))
                        {
                            await handler.OnNotificationAsync(notification, ct);
                        }

                        break;

                    case EventSubMessage.Reconnect reconnect when pending is null:
                        log.LogInformation("EventSub asked to reconnect");
                        pending = await TryOpenAsync(reconnect.ReconnectUrl, frames.Writer, ct);
                        break;

                    case EventSubMessage.Revocation revocation:
                        log.LogWarning(
                            "EventSub subscription {Type} revoked: {Status}",
                            revocation.SubscriptionType,
                            revocation.Status);
                        await handler.OnRevocationAsync(revocation, ct);
                        break;

                    case EventSubMessage.Unknown unknown:
                        log.LogDebug("Ignoring EventSub message type {Type}", unknown.MessageType);
                        break;
                }
            }
        }
        finally
        {
            if (pending is not null)
            {
                await pending.DisposeAsync();
            }

            if (current is not null)
            {
                await current.DisposeAsync();
            }
        }
    }

    /// <summary>Next frame from any connection; every frame restarts the keepalive timer.</summary>
    private async Task<Frame> ReadAsync(ChannelReader<Frame> reader, TimeSpan timeout, CancellationToken ct)
    {
        using var timer = new CancellationTokenSource(timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timer.Token);
        try
        {
            return await reader.ReadAsync(linked.Token);
        }
        catch (OperationCanceledException) when (timer.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new EventSubConnectionLostException($"No EventSub message within {timeout.TotalSeconds:0} s");
        }
    }

    private async Task<Connection?> TryOpenAsync(Uri url, ChannelWriter<Frame> frames, CancellationToken ct)
    {
        try
        {
            return await Connection.OpenAsync(sockets, url, frames, log, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // The old connection keeps working until Twitch closes it; then the caller starts a new session.
            log.LogWarning(ex, "Could not open the EventSub reconnect URL");
            return null;
        }
    }

    /// <param name="Message">The parsed frame, or null when the connection ended (<paramref name="Error"/> says why).</param>
    private sealed record Frame(Connection Source, EventSubMessage? Message, Exception? Error = null);

    /// <summary>A socket plus the task that pumps its frames into the shared channel.</summary>
    private sealed class Connection : IAsyncDisposable
    {
        private readonly IEventSubSocket _socket;
        private readonly CancellationTokenSource _stop = new();
        private Task _pump = Task.CompletedTask;

        private Connection(IEventSubSocket socket) => _socket = socket;

        public static async Task<Connection> OpenAsync(
            IEventSubSocketFactory sockets,
            Uri url,
            ChannelWriter<Frame> frames,
            ILogger log,
            CancellationToken ct)
        {
            var connection = new Connection(await sockets.ConnectAsync(url, ct));
            connection._pump = connection.PumpAsync(frames, log);
            return connection;
        }

        private async Task PumpAsync(ChannelWriter<Frame> frames, ILogger log)
        {
            try
            {
                while (true)
                {
                    var json = await _socket.ReceiveAsync(_stop.Token);
                    if (json is null)
                    {
                        frames.TryWrite(new Frame(this, null, new EventSubConnectionLostException(
                            $"closed by Twitch ({_socket.CloseReason})")));
                        return;
                    }

                    EventSubMessage message;
                    try
                    {
                        message = EventSubMessage.Parse(json);
                    }
                    catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or UriFormatException)
                    {
                        log.LogWarning(ex, "Ignoring malformed EventSub message");
                        continue;
                    }

                    frames.TryWrite(new Frame(this, message));
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                // Closed by us.
            }
            catch (Exception ex)
            {
                frames.TryWrite(new Frame(this, null, ex));
            }
        }

        /// <summary>Closes politely first (cancelling a pending receive would abort the socket), then stops the pump.</summary>
        public async ValueTask DisposeAsync()
        {
            try
            {
                using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _socket.CloseAsync(closeTimeout.Token);
            }
            catch (Exception)
            {
                // Best effort: the socket may already be broken.
            }

            await _stop.CancelAsync();
            await _pump;
            await _socket.DisposeAsync();
            _stop.Dispose();
        }
    }

    /// <summary>Twitch may deliver a notification twice; remembers the last few message ids.</summary>
    private sealed class RecentIds(int capacity)
    {
        private readonly HashSet<string> _set = [];
        private readonly Queue<string> _order = new();

        public bool Add(string id)
        {
            if (!_set.Add(id))
            {
                return false;
            }

            _order.Enqueue(id);
            if (_order.Count > capacity)
            {
                _set.Remove(_order.Dequeue());
            }

            return true;
        }
    }
}
