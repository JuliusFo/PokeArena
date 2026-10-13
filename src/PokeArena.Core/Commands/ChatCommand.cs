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

        var end = trimmed.IndexOfAny([' ', '\t']);
        var name = end < 0 ? trimmed[1..] : trimmed[1..end];
        var arguments = end < 0 ? "" : trimmed[end..].Trim();
        return new ParsedCommand(name.ToLowerInvariant(), arguments);
    }
}
