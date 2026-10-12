namespace PokeArena.Core.Chat;

/// <summary>Text rules of Twitch chat.</summary>
public static class ChatText
{
    /// <summary>Twitch rejects longer messages.</summary>
    public const int MaxLength = 500;

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
