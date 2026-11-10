using Microsoft.EntityFrameworkCore;

namespace PokeArena.Core.Data;

/// <summary>Reading and setting a <see cref="Cooldown"/> row — the boss and every NPC use the same table.</summary>
public static class CooldownExtensions
{
    /// <summary>Until when <paramref name="key"/> is cooling down, or null if it never was.</summary>
    public static Task<DateTimeOffset?> CooldownUntilAsync(this PokeArenaDb db, string key, CancellationToken ct) =>
        db.Cooldowns.Where(c => c.Key == key).Select(c => (DateTimeOffset?)c.Until).FirstOrDefaultAsync(ct);

    /// <summary>Adds or moves the cooldown of <paramref name="key"/>; the caller saves.</summary>
    public static Task SetCooldownAsync(this PokeArenaDb db, string key, DateTimeOffset until, CancellationToken ct) =>
        db.SetCooldownAsync(key, until, null, ct);

    /// <summary>Adds or moves the cooldown of <paramref name="key"/> and records its length (or none); the caller saves.</summary>
    public static async Task SetCooldownAsync(this PokeArenaDb db, string key, DateTimeOffset until, TimeSpan? length, CancellationToken ct)
    {
        if (await db.Cooldowns.FindAsync([key], ct) is { } cooldown)
        {
            cooldown.Until = until;
            cooldown.Length = length;
        }
        else
        {
            db.Cooldowns.Add(new Cooldown { Key = key, Until = until, Length = length });
        }
    }
}
