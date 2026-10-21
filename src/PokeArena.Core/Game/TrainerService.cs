using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokeArena.Core.Chat;
using PokeArena.Core.Data;

namespace PokeArena.Core.Game;

public enum RegistrationStatus
{
    Registered,
    AlreadyRegistered,
}

/// <param name="Starter">The new starter, or the existing one if the trainer was already registered.</param>
public sealed record RegistrationResult(RegistrationStatus Status, Species Starter);

/// <summary>A row of the trainer list in the desktop app.</summary>
/// <param name="Badges">NPC badges earned (spec §7).</param>
public sealed record TrainerSummary(
    string TwitchUserId, string DisplayName, Species Starter, int SpecimenCount, DateTimeOffset RegisteredAt, int Tokens = 0, int Badges = 0);

/// <summary>A trainer found by a chat mention.</summary>
public sealed record TrainerName(string TwitchUserId, string DisplayName, string Login);

/// <summary>
/// Registration and the trainer directory. Commands call it from the single command queue; the UI reads lists
/// from its own thread. Remembers who is registered so that not every chat message hits the database.
/// </summary>
public sealed class TrainerService
{
    private readonly GameDatabase _database;
    private readonly SpeciesCatalog _species;
    private readonly TimeProvider _time;
    private readonly ILogger<TrainerService> _log;

    /// <summary>User id → last known names, or null if not registered. Cleared on a database switch.</summary>
    private readonly ConcurrentDictionary<string, KnownNames?> _known = new();

    public TrainerService(GameDatabase database, SpeciesCatalog species, TimeProvider time, ILogger<TrainerService> log)
    {
        _database = database;
        _species = species;
        _time = time;
        _log = log;
        _database.Switched += () =>
        {
            _known.Clear();
            Changed?.Invoke();
        };
    }

    /// <summary>Raised after trainers were added, renamed or gained specimens, or the database was switched.</summary>
    public event Action? Changed;

    /// <summary>Called by game logic that changed specimens (e.g. boss catches), so lists reload.</summary>
    public void NotifyChanged() => Changed?.Invoke();

    /// <summary>
    /// Registers a viewer with their starter (spec §3): creates the trainer and the protected starter specimen in
    /// one transaction. The choice is final — a second call reports the existing starter.
    /// </summary>
    public async Task<RegistrationResult> RegisterAsync(string twitchUserId, string login, string displayName, Species starter, CancellationToken ct)
    {
        await using var db = _database.Open();
        var existingStarter = await db.Specimens
            .Where(s => s.TrainerId == twitchUserId && s.IsStarter)
            .Select(s => (int?)s.SpeciesId)
            .FirstOrDefaultAsync(ct);
        if (existingStarter is { } speciesId)
        {
            return new RegistrationResult(RegistrationStatus.AlreadyRegistered, _species.Get(speciesId));
        }

        var now = _time.GetUtcNow();
        db.Trainers.Add(new Trainer
        {
            TwitchUserId = twitchUserId,
            DisplayName = displayName,
            Login = login,
            RegisteredAt = now,
            Specimens =
            [
                new Specimen
                {
                    TrainerId = twitchUserId,
                    SpeciesId = starter.Id,
                    CaughtAt = now,
                    Origin = SpecimenOrigin.Starter,
                    IsStarter = true,
                },
            ],
        });
        await db.SaveChangesAsync(ct);

        _known[twitchUserId] = new KnownNames(displayName, login);
        _log.LogInformation("Trainer {UserId} registered with {Starter}", twitchUserId, starter.Slug);
        Changed?.Invoke();
        return new RegistrationResult(RegistrationStatus.Registered, starter);
    }

    /// <summary>
    /// Called for every processed chat message: refreshes the stored display name and login if they changed
    /// (spec §1).
    /// </summary>
    /// <returns>Whether the author is a registered trainer.</returns>
    public async Task<bool> ObserveAsync(ChatMessage message, CancellationToken ct)
    {
        var names = new KnownNames(message.DisplayName, message.Login);
        if (_known.TryGetValue(message.UserId, out var known))
        {
            if (known is null)
            {
                return false;
            }

            if (known == names)
            {
                return true;
            }
        }

        await using var db = _database.Open();
        var trainer = await db.Trainers.FindAsync([message.UserId], ct);
        if (trainer is null)
        {
            _known[message.UserId] = null;
            return false;
        }

        if (trainer.DisplayName != message.DisplayName || trainer.Login != message.Login)
        {
            _log.LogInformation("Trainer {UserId} renamed to {DisplayName} ({Login})", trainer.TwitchUserId, message.DisplayName, message.Login);
            trainer.DisplayName = message.DisplayName;
            trainer.Login = message.Login;
            await db.SaveChangesAsync(ct);
            Changed?.Invoke();
        }

        _known[message.UserId] = names;
        return true;
    }

    /// <summary>
    /// Resolves a chat mention (<c>@name</c> or <c>name</c>) to a trainer: by login first, then by display name,
    /// both case-insensitive. Names are only as current as the trainer's last chat message.
    /// </summary>
    public async Task<TrainerName?> FindByNameAsync(string mention, CancellationToken ct)
    {
        var name = mention.Trim().TrimStart('@').ToLowerInvariant();
        if (name.Length == 0)
        {
            return null;
        }

        await using var db = _database.Open();
        var matches = await db.Trainers
            .Where(t => t.Login == name || t.DisplayName.ToLower() == name)
            .Select(t => new TrainerName(t.TwitchUserId, t.DisplayName, t.Login))
            .ToListAsync(ct);
        return matches.FirstOrDefault(t => t.Login == name) ?? matches.FirstOrDefault();
    }

    /// <summary>All trainers, newest first.</summary>
    public async Task<IReadOnlyList<TrainerSummary>> ListAsync(CancellationToken ct)
    {
        await using var db = _database.Open();
        var rows = await db.Trainers
            .Select(t => new
            {
                t.TwitchUserId,
                t.DisplayName,
                t.RegisteredAt,
                StarterId = t.Specimens.Where(s => s.IsStarter).Select(s => s.SpeciesId).FirstOrDefault(),
                Count = t.Specimens.Count,
                t.Tokens,
                Badges = db.NpcBadges.Count(b => b.TrainerId == t.TwitchUserId),
            })
            .OrderByDescending(t => t.RegisteredAt)
            .ToListAsync(ct);

        return [.. rows.Select(r => new TrainerSummary(
            r.TwitchUserId, r.DisplayName, _species.Get(r.StarterId), r.Count, r.RegisteredAt, r.Tokens, r.Badges))];
    }

    private sealed record KnownNames(string DisplayName, string Login);
}
