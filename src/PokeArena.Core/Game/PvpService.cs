using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokeArena.Core.Chat;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;

namespace PokeArena.Core.Game;

/// <summary>
/// PvP (spec §6): <c>!fight @B</c> opens a challenge, the same command from B within the timeout accepts it, and the
/// fight resolves at once — random teams, the winner takes one specimen of the loser's team (never the starter).
/// Runs only on the command queue, so the open challenges need no lock. They live in memory: lost on restart,
/// expired lazily (silently, no timer) and dropped when the database is switched (dry run).
/// </summary>
public sealed class PvpService
{
    private readonly GameDatabase _database;
    private readonly SpeciesCatalog _species;
    private readonly NpcCatalog _npcs;
    private readonly BattleSettings _battleSettings;
    private readonly PvpSettings _settings;
    private readonly DamageCalculator _damage;
    private readonly ISeedSource _seeds;
    private readonly IChatSender _chat;
    private readonly TrainerService _trainers;
    private readonly BattleService _battles;
    private readonly TimeProvider _time;
    private readonly ILogger<PvpService> _log;

    private readonly List<Challenge> _open = [];

    /// <summary>Bumped on every database switch (from the UI thread); challenges from an older one are stale.</summary>
    private int _generation;

    public PvpService(
        GameDatabase database,
        SpeciesCatalog species,
        NpcCatalog npcs,
        BattleSettings battleSettings,
        PvpSettings settings,
        DamageCalculator damage,
        ISeedSource seeds,
        IChatSender chat,
        TrainerService trainers,
        BattleService battles,
        TimeProvider time,
        ILogger<PvpService> log)
    {
        _database = database;
        _species = species;
        _npcs = npcs;
        _battleSettings = battleSettings;
        _settings = settings;
        _damage = damage;
        _seeds = seeds;
        _chat = chat;
        _trainers = trainers;
        _battles = battles;
        _time = time;
        _log = log;
        _database.Switched += () => Interlocked.Increment(ref _generation);
    }

    /// <summary><c>!fight @name</c> from a registered trainer: challenges <paramref name="arguments"/> or accepts their challenge.</summary>
    public async Task FightAsync(ChatMessage message, string arguments, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var generation = Volatile.Read(ref _generation);
        _open.RemoveAll(c => c.ExpiresAt <= now || c.Generation != generation);

        var mention = "@" + message.DisplayName;
        var name = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (name.TrimStart('@').Length == 0)
        {
            _chat.Send($"{mention}, fordere jemanden zum Kampf heraus – zum Beispiel: !fight @Name");
            return;
        }

        if (await _trainers.FindByNameAsync(name, ct) is not { } target)
        {
            _chat.Send($"{mention}, {name.TrimStart('@')} ist noch kein Trainer.");
            return;
        }

        if (target.TwitchUserId == message.UserId)
        {
            _chat.Send($"{mention}, du kannst dich nicht selbst herausfordern.");
            return;
        }

        if (_open.Find(c => c.ChallengerId == target.TwitchUserId && c.TargetId == message.UserId) is { } pending)
        {
            _open.Remove(pending);
            await AcceptAsync(pending, message.DisplayName, now, ct);
            return;
        }

        if (_open.Exists(c => c.Involves(message.UserId)))
        {
            _chat.Send($"{mention}, du hast schon eine offene Herausforderung.");
            return;
        }

        if (_open.Exists(c => c.Involves(target.TwitchUserId)))
        {
            _chat.Send($"{mention}, {target.DisplayName} hat schon eine offene Herausforderung.");
            return;
        }

        var counts = await CountSpecimensAsync([message.UserId, target.TwitchUserId], ct);
        if (TooFew(counts, message.UserId, "du hast", target.TwitchUserId, $"{target.DisplayName} hat") is { } tooFew)
        {
            _chat.Send($"{mention}, {tooFew}");
            return;
        }

        _open.Add(new Challenge(message.UserId, message.DisplayName, target.TwitchUserId, now + _settings.ChallengeTimeout, generation));
        _log.LogInformation("PvP challenge from {Challenger} to {Target}", message.UserId, target.TwitchUserId);
        _chat.Send(
            $"@{target.DisplayName}, {message.DisplayName} fordert dich zum Kampf heraus! Nimm an mit !fight @{message.Login} – " +
            $"du hast {Minutes(_settings.ChallengeTimeout)}.");
    }

    private async Task AcceptAsync(Challenge challenge, string targetName, DateTimeOffset now, CancellationToken ct)
    {
        // Inventories may have changed since the challenge (boss catches, other fights): check again.
        var counts = await CountSpecimensAsync([challenge.ChallengerId, challenge.TargetId], ct);
        if (TooFew(counts, challenge.ChallengerId, $"{challenge.ChallengerName} hat", challenge.TargetId, $"{targetName} hat") is { } tooFew)
        {
            _chat.Send($"Der Kampf fällt aus: {tooFew}");
            return;
        }

        await using var db = _database.Open();
        var ids = new[] { challenge.ChallengerId, challenge.TargetId };
        var specimens = await db.Specimens.AsNoTracking().Where(s => ids.Contains(s.TrainerId)).OrderBy(s => s.Id).ToListAsync(ct);

        // Everything random — both teams (challenger first), the hits, the loot — comes from one seed.
        var seed = _seeds.NextSeed();
        var random = new BattleRandom(seed);
        var challengerTeam = DrawTeam(specimens, challenge.ChallengerId, random);
        var opponentTeam = DrawTeam(specimens, challenge.TargetId, random);
        var result = new TeamBattle(_damage).Fight(
            Fighters(challengerTeam, challenge.ChallengerId, challenge.ChallengerName),
            Fighters(opponentTeam, challenge.TargetId, targetName),
            random);
        if (result.HitRoundLimit)
        {
            _log.LogWarning("PvP fight hit the round limit (seed {Seed})", seed);
        }

        var (winnerId, winnerName, loserName, loserTeam) = result.ChallengerWon
            ? (challenge.ChallengerId, challenge.ChallengerName, targetName, opponentTeam)
            : (challenge.TargetId, targetName, challenge.ChallengerName, challengerTeam);

        // A team of at least two always holds a non-starter: only one specimen per trainer is the starter.
        var loot = random.Pick(loserTeam.Where(s => !s.IsStarter).ToList());

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Battles.Add(new Battle
        {
            Kind = BattleKind.Pvp,
            FoughtAt = now,
            Seed = seed,
            LootSpecimenId = loot.Id,
            LootSpeciesId = loot.SpeciesId,
            Outcome = result.ChallengerWon ? BattleOutcome.ChallengerWon : BattleOutcome.OpponentWon,
            Rounds = (result.Attacks + 1) / 2,
            LogJson = result.Log.ToJson(),
            Participants =
            [
                new BattleParticipant { TrainerId = challenge.ChallengerId, Won = result.ChallengerWon },
                new BattleParticipant { TrainerId = challenge.TargetId, Won = !result.ChallengerWon },
            ],
        });
        var transferred = await db.Specimens.SingleAsync(s => s.Id == loot.Id, ct);
        transferred.TrainerId = winnerId;
        await db.SaveChangesAsync(ct);

        var achievements = await Achievements.UnlockNewAsync(db, [winnerId], _npcs, now, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        _log.LogInformation(
            "PvP fight: {Challenger} vs {Target}, winner {Winner}, loot specimen {Loot}, seed {Seed}",
            challenge.ChallengerId, challenge.TargetId, winnerId, loot.Id, seed);

        _chat.Send($"{winnerName} besiegt {loserName} und bekommt {_species.Get(loot.SpeciesId).Name} von {loserName}!");
        foreach (var a in achievements)
        {
            _chat.Send($"{a.DisplayName} hat AV {a.Npc.Threshold} freigeschalten und kann NPC {a.Npc.Name} herausfordern.");
        }

        _battles.NotifyChanged();
        _trainers.NotifyChanged();
    }

    private List<Specimen> DrawTeam(List<Specimen> specimens, string trainerId, BattleRandom random) =>
        [.. random.Shuffled(specimens.Where(s => s.TrainerId == trainerId)).Take(_settings.TeamSize)];

    private List<Fighter> Fighters(List<Specimen> team, string trainerId, string name) =>
        [.. team.Select(s => Fighter.Create(_species.Get(s.SpeciesId), _battleSettings, trainerId, name))];

    private async Task<Dictionary<string, int>> CountSpecimensAsync(string[] trainerIds, CancellationToken ct)
    {
        await using var db = _database.Open();
        return await db.Specimens
            .Where(s => trainerIds.Contains(s.TrainerId))
            .GroupBy(s => s.TrainerId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);
    }

    /// <summary>The reason text if one side has fewer specimens than a team needs, checking the first side first.</summary>
    private string? TooFew(Dictionary<string, int> counts, string firstId, string firstHas, string secondId, string secondHas)
    {
        foreach (var (id, has) in new[] { (firstId, firstHas), (secondId, secondHas) })
        {
            var count = counts.GetValueOrDefault(id);
            if (count < _settings.TeamSize)
            {
                return $"{has} erst {count} Pokémon – für einen Kampf braucht ihr beide mindestens {_settings.TeamSize}.";
            }
        }

        return null;
    }

    private static string Minutes(TimeSpan span) =>
        span.TotalMinutes is var minutes && minutes == 1 ? "1 Minute" : $"{minutes:0} Minuten";

    private sealed record Challenge(string ChallengerId, string ChallengerName, string TargetId, DateTimeOffset ExpiresAt, int Generation)
    {
        public bool Involves(string trainerId) => ChallengerId == trainerId || TargetId == trainerId;
    }
}
