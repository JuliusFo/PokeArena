using System.Net.WebSockets;
using System.Text;

namespace PokeArena.Core.Twitch.EventSub;

/// <summary>Receive side of one EventSub WebSocket connection; a seam so the session logic is testable.</summary>
public interface IEventSubSocket : IAsyncDisposable
{
    /// <returns>The next text frame, or null once the server has closed the connection.</returns>
    Task<string?> ReceiveAsync(CancellationToken ct);

    /// <summary>Close code and reason sent by the server, once <see cref="ReceiveAsync"/> returned null.</summary>
    string? CloseReason { get; }

    Task CloseAsync(CancellationToken ct);
}

public interface IEventSubSocketFactory
{
    Task<IEventSubSocket> ConnectAsync(Uri url, CancellationToken ct);
}

public sealed class ClientWebSocketFactory : IEventSubSocketFactory
{
    public async Task<IEventSubSocket> ConnectAsync(Uri url, CancellationToken ct)
    {
        var socket = new ClientWebSocket();
        try
        {
            await socket.ConnectAsync(url, ct);
            return new ClientEventSubSocket(socket);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Wraps <see cref="ClientWebSocket"/>, which answers Twitch's pings by itself. The client never sends data:
    /// anything but a pong makes Twitch close the connection (4001).
    /// </summary>
    private sealed class ClientEventSubSocket(ClientWebSocket socket) : IEventSubSocket
    {
        /// <summary>EventSub frames are a few KB; a bigger one means something is wrong.</summary>
        private const int MaxMessageBytes = 1024 * 1024;

        private readonly byte[] _buffer = new byte[16 * 1024];

        public string? CloseReason { get; private set; }

        public async Task<string?> ReceiveAsync(CancellationToken ct)
        {
            using var message = new MemoryStream();
            while (true)
            {
                var result = await socket.ReceiveAsync(_buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    CloseReason = $"{(int?)socket.CloseStatus} {socket.CloseStatusDescription}".Trim();
                    return null;
                }

                message.Write(_buffer, 0, result.Count);
                if (message.Length > MaxMessageBytes)
                {
                    throw new WebSocketException($"EventSub message larger than {MaxMessageBytes} bytes");
                }

                if (result.EndOfMessage)
                {
                    return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                }
            }
        }

        public async Task CloseAsync(CancellationToken ct)
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct);
            }
        }

        public ValueTask DisposeAsync()
        {
            socket.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
