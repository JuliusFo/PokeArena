using System.Threading.Channels;

namespace PokeArena.Core.Chat;

/// <summary>An entry of the command queue: a <see cref="ChatMessage"/> or <see cref="QueuedWork"/>.</summary>
public abstract record InboxItem;

/// <summary>
/// Internal work that must run between chat commands, e.g. a boss lobby whose countdown ended. Going through the
/// queue keeps game state single-threaded.
/// </summary>
/// <param name="Name">For the log if the work fails.</param>
public sealed record QueuedWork(string Name, Func<CancellationToken, Task> RunAsync) : InboxItem;

/// <summary>
/// The single queue all incoming chat messages and internal work pass through. One consumer processes them one
/// after another, so game commands never race each other (fights resolve instantly, the queue costs nothing).
/// </summary>
public sealed class ChatInbox
{
    private readonly Channel<InboxItem> _items = Channel.CreateUnbounded<InboxItem>(
        new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<InboxItem> Reader => _items.Reader;

    public void Post(InboxItem item) => _items.Writer.TryWrite(item);
}
