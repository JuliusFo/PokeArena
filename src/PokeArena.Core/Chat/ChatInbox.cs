using System.Threading.Channels;

namespace PokeArena.Core.Chat;

/// <summary>
/// The single queue all incoming chat messages pass through. One consumer processes them one after another, so
/// game commands never race each other (fights resolve instantly, the queue costs nothing).
/// </summary>
public sealed class ChatInbox
{
    private readonly Channel<ChatMessage> _messages = Channel.CreateUnbounded<ChatMessage>(
        new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<ChatMessage> Reader => _messages.Reader;

    public void Post(ChatMessage message) => _messages.Writer.TryWrite(message);
}
