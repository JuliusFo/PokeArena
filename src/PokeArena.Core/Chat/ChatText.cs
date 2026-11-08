namespace PokeArena.Core.Chat;

/// <summary>Text rules of Twitch chat.</summary>
public static class ChatText
{
    /// <summary>Twitch rejects longer messages.</summary>
    public const int MaxLength = 500;

    /// <summary>Longest name the bot repeats back (Twitch logins have at most 25 characters).</summary>
    private const int MaxEchoLength = 25;

    /// <summary>
    /// The words of a command's arguments, split at any whitespace — not only U+0020, or a no-break space would turn
    /// a whole sentence into one "name".
    /// </summary>
    public static string[] Words(string arguments) =>
        arguments.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// „46 Stunden 30 Minuten“, „1 Stunde“, „5 Minuten“ — rounded up to whole minutes, at least one. The one way the
    /// bot writes a duration, so a cooldown reads the same in every reply (review 1.0.3, B4).
    /// </summary>
    public static string Duration(TimeSpan span)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(span.TotalMinutes));
        var (h, m) = (minutes / 60, minutes % 60);
        var hours = h switch { 0 => null, 1 => "1 Stunde", _ => $"{h} Stunden" };
        var rest = m switch { 0 => null, 1 => "1 Minute", _ => $"{m} Minuten" };
        return string.Join(' ', new[] { hours, rest }.OfType<string>());
    }

    /// <summary>The first word of a command's arguments, or "".</summary>
    public static string FirstWord(string arguments) => Words(arguments).FirstOrDefault() ?? "";

    /// <summary>
    /// A name typed by a viewer, safe to repeat in a bot message: without a leading "@", only letters, digits and
    /// "_", at most 25 characters. Anything else is not repeated (review 1.0.3, A18), so the bot never posts a
    /// viewer's sentence, mention or link under its own name.
    /// </summary>
    public static string? Echo(string word)
    {
        var name = word.TrimStart('@');
        return name.Length is > 0 and <= MaxEchoLength && name.All(c => char.IsLetterOrDigit(c) || c == '_') ? name : null;
    }

    /// <summary>
    /// Splits <paramref name="text"/> into messages of at most <paramref name="maxLength"/> characters, preferring
    /// breaks at spaces and never cutting a surrogate pair (emoji) in half.
    /// </summary>
    public static IReadOnlyList<string> Split(string text, int maxLength = MaxLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 2);
        var parts = new List<string>();
        var rest = text.AsSpan().Trim();
        while (rest.Length > maxLength)
        {
            var cut = rest[..(maxLength + 1)].LastIndexOf(' ');
            if (cut <= 0)
            {
                cut = char.IsHighSurrogate(rest[maxLength - 1]) ? maxLength - 1 : maxLength;
            }

            parts.Add(rest[..cut].TrimEnd().ToString());
            rest = rest[cut..].TrimStart();
        }

        if (!rest.IsEmpty)
        {
            parts.Add(rest.ToString());
        }

        return parts;
    }
}
