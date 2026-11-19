using PokeArena.Core.Chat;

namespace PokeArena.Core.Commands;

public enum CommandAccess
{
    Everyone,

    /// <summary>Registered trainers; everyone else gets a hint to register (spec §3).</summary>
    Registered,

    Moderators,
}

/// <param name="Arguments">Everything after the command name, trimmed.</param>
public sealed record CommandContext(ChatMessage Message, string Arguments, IChatSender Chat);

/// <summary>A chat command such as <c>!pokearena</c>. Runs on the single command queue (<see cref="ChatInbox"/>).</summary>
public interface IChatCommand
{
    /// <summary>Lower-case name without the "!".</summary>
    string Name { get; }

    /// <summary>Further lower-case names without the "!" that run the same command (e.g. <c>!version</c>).</summary>
    IReadOnlyList<string> Aliases => [];

    CommandAccess Access { get; }

    Task ExecuteAsync(CommandContext context, CancellationToken ct);
}

/// <summary>A parsed "!name arguments" message.</summary>
public sealed record ParsedCommand(string Name, string Arguments)
{
    /// <summary>
    /// Chat clients like 7TV append an invisible tag character (U+E0000) to repeated messages to dodge Twitch's
    /// duplicate filter; it must not become part of the command.
    /// </summary>
    private const string InvisibleSuffix = "\U000E0000";

    public static ParsedCommand? TryParse(string text)
    {
        var trimmed = text.Replace(InvisibleSuffix, "", StringComparison.Ordinal).Trim();
        if (trimmed.Length < 2 || trimmed[0] != '!' || char.IsWhiteSpace(trimmed[1]))
        {
            return null;
        }

        // Any whitespace ends the name, as it separates the arguments (review 1.8.1, A12): „!fight<U+3000>@ash“ from an
        // IME is a fight, not an unknown command.
        var end = trimmed.AsSpan().IndexOfAny(WhiteSpace);
        var name = end < 0 ? trimmed[1..] : trimmed[1..end];
        var arguments = end < 0 ? "" : trimmed[end..].Trim();
        return new ParsedCommand(name.ToLowerInvariant(), arguments);
    }

    /// <summary>Every character <see cref="char.IsWhiteSpace(char)"/> accepts, as <c>ChatText.Words</c> splits.</summary>
    private static readonly System.Buffers.SearchValues<char> WhiteSpace = System.Buffers.SearchValues.Create(
        [.. Enumerable.Range(0, char.MaxValue + 1).Select(c => (char)c).Where(char.IsWhiteSpace)]);
}
