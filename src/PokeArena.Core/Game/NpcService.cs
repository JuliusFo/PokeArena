using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokeArena.Core.Chat;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;

namespace PokeArena.Core.Game;

/// <summary>
/// NPC fights (spec §7): <c>!challenge &lt;NPC&gt;</c> against an NPC unlocked by an achievement resolves at once —
/// a random team of the trainer against the NPC's fixed team. Every win brings the NPC's tokens, the first one also
/// its badge and a specimen of its team (E22). Cooldowns per trainer and NPC are stored in the database and
/// survive a restart; the dry run skips them. Runs only on the command queue.
/// </summary>
public sealed class NpcService(
    GameDatabase database,
    SpeciesCatalog species,
    NpcCatalog npcs,
    BattleSettings battleSettings,
    PvpSettings pvpSettings,
    NpcSettings settings,
    DamageCalculator damage,
    ISeedSource seeds,
    IChatSender chat,
    TrainerService trainers,
    BattleService battles,
    TimeProvider time,
    ILogger<NpcService> log)
{
    /// <summary>The <see cref="Cooldown"/> key of a trainer and an NPC.</summary>
    public static string CooldownKey(string trainerId, string npcId) => $"npc:{trainerId}:{npcId}";

    /// <summary><c>!challenge &lt;NPC&gt;</c> from a registered trainer.</summary>
    public async Task ChallengeAsync(ChatMessage message, string arguments, CancellationToken ct)
    {
        var mention = "@" + message.DisplayName;
        var name = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

        await using var db = database.Open();
        var unlocked = (await db.AchievementUnlocks.Where(u => u.TrainerId == message.UserId).Select(u => u.Threshold).ToListAsync(ct))
            .ToHashSet();
        if (name.Length == 0 || npcs.Find(name) is not { } npc)
        {
            var available = npcs.All.Where(n => unlocked.Contains(n.Threshold)).Select(n => n.Name).ToList();
            var start = name.Length == 0 ? $"{mention}, fordere einen NPC heraus" : $"{mention}, einen NPC namens {name} gibt es nicht";
            chat.Send(available.Count == 0
                ? $"{start}. Den ersten ({npcs.All[0].Name}) schaltest du mit {npcs.All[0].Threshold} verschiedenen Pokémon frei."
                : $"{start} – zum Beispiel: !challenge {available[0]}. Freigeschaltet: {string.Join(", ", available)}.");
            return;
        }

        if (!unlocked.Contains(npc.Threshold))
        {
            var distinct = await db.Specimens.Where(s => s.TrainerId == message.UserId).Select(s => s.SpeciesId).Distinct().CountAsync(ct);
            chat.Send($"{mention}, du kannst {npc.Name} noch nicht herausfordern (noch {Math.Max(1, npc.Threshold - distinct)} Pokémon benötigt).");
            return;
        }

        var now = time.GetUtcNow();
        var cooldownKey = CooldownKey(message.UserId, npc.Id);
        if (!message.IsSimulated
            && await db.Cooldowns.Where(c => c.Key == cooldownKey).Select(c => (DateTimeOffset?)c.Until).FirstOrDefaultAsync(ct) is { } until
            && until > now)
        {
            chat.Send($"{mention}, du kannst {npc.Name} noch nicht herausfordern (Cooldown {HoursAndMinutes(until - now)}).");
            return;
        }

        var specimens = await db.Specimens.AsNoTracking().Where(s => s.TrainerId == message.UserId).OrderBy(s => s.Id).ToListAsync(ct);
        if (specimens.Count < pvpSettings.TeamSize)
        {
            chat.Send($"{mention}, du hast erst {specimens.Count} Pokémon – für einen Kampf brauchst du mindestens {pvpSettings.TeamSize}.");
            return;
        }

        await FightAsync(db, message, npc, specimens, now, ct);
    }

    private async Task FightAsync(PokeArenaDb db, ChatMessage message, Npc npc, List<Specimen> specimens, DateTimeOffset now, CancellationToken ct)
    {
        // Everything random — the trainer's team, the hits, the reward specimen — comes from one seed.
        var seed = seeds.NextSeed();
        var random = new BattleRandom(seed);
        var team = random.Shuffled(specimens).Take(pvpSettings.TeamSize).ToList();
        var result = new TeamBattle(damage).Fight(
            [.. team.Select(s => Fighter.Create(species.Get(s.SpeciesId), battleSettings, message.UserId, message.DisplayName))],
            [.. npc.Team.Select(s => Fighter.Create(s, battleSettings, null, npc.Name, strength: npc.Strength))],
            random);
        if (result.HitRoundLimit)
        {
            log.LogWarning("NPC fight hit the round limit (seed {Seed}, NPC {Npc})", seed, npc.Id);
        }

        var won = result.ChallengerWon;
        var firstWin = won && !await db.NpcBadges.AnyAsync(b => b.TrainerId == message.UserId && b.NpcId == npc.Id, ct);
        Species? reward = null;
        if (firstWin)
        {
            var owned = specimens.Select(s => s.SpeciesId).ToHashSet();
            var missing = npc.Team.Where(s => !owned.Contains(s.Id)).ToList();
            reward = random.Pick(missing.Count > 0 ? missing : npc.Team);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        Specimen? rewardSpecimen = null;
        if (reward is not null)
        {
            rewardSpecimen = new Specimen { TrainerId = message.UserId, SpeciesId = reward.Id, CaughtAt = now, Origin = SpecimenOrigin.Npc };
            db.Specimens.Add(rewardSpecimen);
            await db.SaveChangesAsync(ct);
        }

        var battle = new Battle
        {
            Kind = BattleKind.Npc,
            FoughtAt = now,
            Seed = seed,
            NpcId = npc.Id,
            LootSpecimenId = rewardSpecimen?.Id,
            LootSpeciesId = reward?.Id,
            Tokens = won ? npc.Tokens : null,
            Outcome = won ? BattleOutcome.ChallengerWon : BattleOutcome.OpponentWon,
            Rounds = (result.Attacks + 1) / 2,
            LogJson = result.Log.ToJson(),
            Participants = [new BattleParticipant { TrainerId = message.UserId, Won = won }],
        };
        db.Battles.Add(battle);
        if (won)
        {
            var trainer = await db.Trainers.SingleAsync(t => t.TwitchUserId == message.UserId, ct);
            trainer.Tokens += npc.Tokens;
        }

        await db.SaveChangesAsync(ct);

        if (firstWin)
        {
            db.NpcBadges.Add(new NpcBadge { TrainerId = message.UserId, NpcId = npc.Id, WonAt = now, BattleId = battle.Id });
        }

        IReadOnlyList<NewAchievement> achievements = reward is null ? [] : await Achievements.UnlockNewAsync(db, [message.UserId], npcs, now, ct);
        TimeSpan? cooldown = message.IsSimulated ? null : won ? settings.WinCooldown : settings.LossCooldown;
        if (cooldown is { } span)
        {
            await SetCooldownAsync(db, CooldownKey(message.UserId, npc.Id), now + span, ct);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        log.LogInformation(
            "NPC fight: {Trainer} vs {Npc}, {Outcome}, reward {Reward}, {Tokens} tokens, seed {Seed}",
            message.UserId, npc.Id, won ? "won" : "lost", reward?.Slug, won ? npc.Tokens : 0, seed);

        chat.Send(won
            ? WinText(message.DisplayName, npc, firstWin, reward)
            : $"{npc.Name} besiegt {message.DisplayName}!" + (cooldown is { } wait ? $" Nächster Versuch in {Duration(wait)}." : ""));
        foreach (var a in achievements)
        {
            chat.Send($"{a.DisplayName} hat AV {a.Npc.Threshold} freigeschalten und kann NPC {a.Npc.Name} herausfordern.");
        }

        battles.NotifyChanged();
        trainers.NotifyChanged();
    }

    private static string WinText(string trainer, Npc npc, bool firstWin, Species? reward)
    {
        var gains = new List<string>();
        if (firstWin)
        {
            gains.Add($"den Orden von {npc.Name}");
            gains.Add(reward!.Name);
        }

        if (npc.Tokens > 0)
        {
            gains.Add(npc.Tokens == 1 ? "1 Token" : $"{npc.Tokens} Tokens");
        }

        var beats = firstWin ? $"{trainer} besiegt {npc.Name}" : $"{trainer} besiegt {npc.Name} erneut";
        return gains.Count switch
        {
            0 => beats + "!",
            1 => $"{beats} und bekommt {gains[0]}!",
            _ => $"{beats} und bekommt {string.Join(", ", gains[..^1])} und {gains[^1]}!",
        };
    }

    private static async Task SetCooldownAsync(PokeArenaDb db, string key, DateTimeOffset until, CancellationToken ct)
    {
        var cooldown = await db.Cooldowns.FindAsync([key], ct);
        if (cooldown is null)
        {
            db.Cooldowns.Add(new Cooldown { Key = key, Until = until });
        }
        else
        {
            cooldown.Until = until;
        }
    }

    /// <summary>„47:59“: hours and minutes, rounded up to the next minute.</summary>
    private static string HoursAndMinutes(TimeSpan remaining)
    {
        var minutes = (int)Math.Ceiling(remaining.TotalMinutes);
        return $"{minutes / 60}:{minutes % 60:00}";
    }

    private static string Duration(TimeSpan span) => span switch
    {
        { TotalMinutes: < 60 } or { Minutes: not 0 } => span.TotalMinutes == 1 ? "1 Minute" : $"{span.TotalMinutes:0} Minuten",
        { TotalHours: 1 } => "1 Stunde",
        _ => $"{span.TotalHours:0} Stunden",
    };
}
