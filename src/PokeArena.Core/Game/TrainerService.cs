using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokeArena.Contracts;
using PokeArena.Core.Chat;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Publishing;

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
public sealed record TrainerName(string TwitchUserId, string DisplayName, string Login)
{
    /// <summary>What a viewer types to name this trainer: the login, or the display name once the login was released.</summary>
    public string Handle => Login.Length > 0 ? Login : DisplayName;
}

/// <param name="Trainer">The one trainer the name means, or null.</param>
/// <param name="Ambiguous">No trainer because several match (same display name, no login match).</param>
public sealed record NameMatch(TrainerName? Trainer, bool Ambiguous);

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

    /// <summary>Raised on the command queue after a trainer was deleted, with their Twitch user id.</summary>
    public event Action<string>? Deleted;

    /// <summary>Called by game logic that changed specimens (e.g. boss catches), so lists reload.</summary>
    public void NotifyChanged() => Changed?.Invoke();

    /// <summary>
    /// Registers a viewer with their starter (spec §3): creates the trainer and the protected starter specimen in
    /// one transaction. The choice is final — a second call reports the existing starter.
    /// </summary>
    public async Task<RegistrationResult> RegisterAsync(string twitchUserId, string login, string displayName, Species starter, CancellationToken ct)
    {
        await using var db = _database.Open();
        // BEGIN IMMEDIATE: the publish worker must not delete an outbox row between our read and our update of it
        // (review 1.0.3, A25).
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
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
                    AcquiredAt = now,
                    Origin = SpecimenOrigin.Starter,
                    IsStarter = true,
                },
            ],
        });
        await ReleaseLoginAsync(db, login, twitchUserId, ct);
        await SiteOutbox.EnqueueAsync(db, SiteOutbox.ForTrainers([twitchUserId]), now, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _known[twitchUserId] = new KnownNames(displayName, login, null);
        _log.LogInformation("Trainer {UserId} registered with {Starter}", twitchUserId, starter.Slug);
        Changed?.Invoke();
        return new RegistrationResult(RegistrationStatus.Registered, starter);
    }

    /// <summary>
    /// Called for every processed chat message: refreshes the stored display name and login if they changed
    /// (spec §1), and the chat colour if the message has one.
    /// </summary>
    /// <returns>Whether the author is a registered trainer.</returns>
    public async Task<bool> ObserveAsync(ChatMessage message, CancellationToken ct)
    {
        if (_known.TryGetValue(message.UserId, out var known))
        {
            if (known is null)
            {
                return false;
            }

            if (known.DisplayName == message.DisplayName && known.Login == message.Login && (message.Color is null || known.Color == message.Color))
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
            // Only the id: names in the log file would outlive a deletion (review 1.0.3, A20).
            _log.LogInformation("Trainer {UserId} changed their name", trainer.TwitchUserId);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            trainer.DisplayName = message.DisplayName;
            trainer.Login = message.Login;
            trainer.ChatColor = message.Color ?? trainer.ChatColor;
            await ReleaseLoginAsync(db, message.Login, trainer.TwitchUserId, ct);
            await SiteOutbox.EnqueueAsync(db, SiteOutbox.ForTrainers([trainer.TwitchUserId]), _time.GetUtcNow(), ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            Changed?.Invoke();
        }
        else if (message.Color is { } color && trainer.ChatColor != color)
        {
            // Only fight pages show the colour; they pick it up with the next fight or „Alles neu veröffentlichen“.
            trainer.ChatColor = color;
            await db.SaveChangesAsync(ct);
        }

        _known[message.UserId] = new KnownNames(trainer.DisplayName, trainer.Login, trainer.ChatColor);
        return true;
    }

    /// <summary>
    /// Resolves a chat mention (<c>@name</c> or <c>name</c>) to a trainer: by login first, then by display name,
    /// both case-insensitive. Names are only as current as the trainer's last chat message.
    /// </summary>
    public async Task<TrainerName?> FindByNameAsync(string mention, CancellationToken ct) =>
        (await ResolveNameAsync(mention, ct)).Trainer;

    /// <summary>Like <see cref="FindByNameAsync"/>, and says whether a miss was because several trainers matched.</summary>
    public async Task<NameMatch> ResolveNameAsync(string mention, CancellationToken ct)
    {
        var name = mention.Trim().TrimStart('@').ToLowerInvariant();
        if (name.Length == 0)
        {
            return new NameMatch(null, Ambiguous: false);
        }

        await using var db = _database.Open();
        var matches = await db.Trainers
            .Where(t => t.Login == name || t.DisplayName.ToLower() == name)
            .Select(t => new TrainerName(t.TwitchUserId, t.DisplayName, t.Login))
            .ToListAsync(ct);

        // Logins are unique (ReleaseLoginAsync). Two trainers with the same display name are never guessed between:
        // a wrong guess could delete the wrong trainer (review 1.0.3, A14).
        var byLogin = matches.Where(t => t.Login == name).ToList();
        var candidates = byLogin.Count > 0 ? byLogin : matches;
        return candidates.Count == 1 ? new NameMatch(candidates[0], Ambiguous: false) : new NameMatch(null, candidates.Count > 1);
    }

    /// <summary>
    /// A Twitch login belongs to one account at a time. A trainer who renamed and has not chatted since still has
    /// the old login stored; when someone else now shows up with it, the old entry is stale and is cleared, so
    /// <c>@login</c> never finds two trainers (review 1.0.3, A14).
    /// </summary>
    private static Task ReleaseLoginAsync(PokeArenaDb db, string login, string ownerId, CancellationToken ct) =>
        db.Trainers
            .Where(t => t.Login == login && t.TwitchUserId != ownerId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Login, ""), ct);

    /// <summary>
    /// Deletes a trainer for good (spec §11): specimens, achievements, badges, NPC cooldowns and fight
    /// participations go in one transaction, and in the stored logs of their fights name and id become
    /// „[gelöscht]“ (WP-08 decision). Queues every site file that showed the name: the trainer page (removed by the
    /// publish worker), the lists, the fights and the pages of the other trainers in them. Runs on the command
    /// queue.
    /// </summary>
    /// <returns>False if there is no such trainer.</returns>
    public async Task<bool> DeleteAsync(string twitchUserId, CancellationToken ct)
    {
        await using var db = _database.Open();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var trainer = await db.Trainers.FindAsync([twitchUserId], ct);
        if (trainer is null)
        {
            return false;
        }

        var battleIds = await db.BattleParticipants
            .Where(p => p.TrainerId == twitchUserId)
            .Select(p => p.BattleId)
            .Distinct()
            .ToListAsync(ct);
        var others = await db.BattleParticipants
            .Where(p => battleIds.Contains(p.BattleId) && p.TrainerId != twitchUserId)
            .Select(p => p.TrainerId)
            .Distinct()
            .ToListAsync(ct);
        foreach (var battle in await db.Battles.Where(b => battleIds.Contains(b.Id)).ToListAsync(ct))
        {
            battle.LogJson = BattleLog.FromJson(battle.LogJson).WithoutTrainer(twitchUserId).ToJson();
        }

        var npcCooldowns = NpcService.CooldownKey(twitchUserId, "");
        await db.BattleParticipants.Where(p => p.TrainerId == twitchUserId).ExecuteDeleteAsync(ct);
        await db.Specimens.Where(s => s.TrainerId == twitchUserId).ExecuteDeleteAsync(ct);
        await db.AchievementUnlocks.Where(u => u.TrainerId == twitchUserId).ExecuteDeleteAsync(ct);
        await db.NpcBadges.Where(b => b.TrainerId == twitchUserId).ExecuteDeleteAsync(ct);
        await db.Cooldowns.Where(c => c.Key.StartsWith(npcCooldowns)).ExecuteDeleteAsync(ct);
        db.Trainers.Remove(trainer);
        await SiteOutbox.EnqueueAsync(
            db,
            [SitePaths.BattleIndex, .. SiteOutbox.ForTrainers([twitchUserId, .. others]), .. battleIds.Select(SitePaths.Battle)],
            _time.GetUtcNow(),
            ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _known[twitchUserId] = null;
        _log.LogInformation("Trainer {UserId} deleted ({Battles} fights anonymized)", twitchUserId, battleIds.Count);
        Deleted?.Invoke(twitchUserId);
        Changed?.Invoke();
        return true;
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

    private sealed record KnownNames(string DisplayName, string Login, string? Color);
}
