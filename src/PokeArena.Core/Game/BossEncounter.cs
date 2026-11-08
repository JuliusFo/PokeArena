using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokeArena.Contracts;
using PokeArena.Core.Chat;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Publishing;
using PokeArena.Core.Twitch;

namespace PokeArena.Core.Game;

/// <summary>
/// The boss encounter (spec §4): idle → <c>!boss</c> opens a lobby → countdown, others join → the fight resolves
/// instantly → cooldown. Runs only on the command queue (commands and <see cref="QueuedWork"/>), so the lobby
/// needs no lock. The cooldown is stored in the database and survives a restart; an open lobby does not.
/// In the dry run cooldowns are skipped, so the streamer can test again right away. When the cooldown after a
/// fight runs out, the chat hears that the boss is ready again — unless the app was restarted meanwhile.
/// </summary>
public sealed class BossEncounter(
    GameDatabase database,
    SpeciesCatalog species,
    NpcCatalog npcs,
    BattleSettings battleSettings,
    BossSettings settings,
    DamageCalculator damage,
    ISeedSource seeds,
    ChatInbox inbox,
    IChatSender chat,
    TrainerService trainers,
    BattleService battles,
    SiteLinks links,
    DryRunMode dryRun,
    StreamStatus stream,
    TimeProvider time,
    ILogger<BossEncounter> log)
{
    public const string CooldownKey = "boss";

    internal const string ReadyAnnouncement = "Der Boss ist wieder bereit – schreibt !boss, um ihn zu rufen!";

    /// <summary>Cooldown replies go out at most this often, so a spamming chat does not fill the send queue.</summary>
    public static readonly TimeSpan CooldownReplyInterval = TimeSpan.FromSeconds(30);

    private Lobby? _lobby;
    private long _lobbyNumber;
    private DateTimeOffset? _lastCooldownReply;

    /// <summary><c>!boss</c> from a registered trainer: opens a lobby or joins the open one.</summary>
    public async Task JoinAsync(ChatMessage message, CancellationToken ct)
    {
        // A lobby from the other mode is stale (the dry run was switched while it was open).
        if (_lobby is { } stale && stale.IsSimulated != message.IsSimulated)
        {
            _lobby = null;
        }

        if (_lobby is { } open)
        {
            open.Join(message.UserId, message.DisplayName);
            return;
        }

        var now = time.GetUtcNow();
        if (!message.IsSimulated && await CooldownUntilAsync(ct) is { } until && until > now)
        {
            ReplyCooldown(message, until - now, now);
            return;
        }

        var lobby = new Lobby(++_lobbyNumber, message.IsSimulated);
        lobby.Join(message.UserId, message.DisplayName);
        _lobby = lobby;
        log.LogInformation("Boss lobby {Number} opened by {UserId}", lobby.Number, message.UserId);
        chat.Send($"Ein wilder Boss nähert sich! Schreibt !boss, um mitzukämpfen – noch {settings.Countdown.TotalSeconds:0} Sekunden.");
        _ = CloseAfterCountdownAsync(lobby.Number, ct);
    }

    private async Task CloseAfterCountdownAsync(long number, CancellationToken ct)
    {
        try
        {
            await Task.Delay(settings.Countdown, time, ct);
            inbox.Post(new QueuedWork("Boss-Kampf", workCt => CloseLobbyAsync(number, workCt)));
        }
        catch (OperationCanceledException)
        {
            // App shutting down: the open lobby is simply dropped.
        }
    }

    /// <summary>Runs on the command queue once the countdown of lobby <paramref name="number"/> has ended.</summary>
    internal async Task CloseLobbyAsync(long number, CancellationToken ct)
    {
        if (_lobby is not { } lobby || lobby.Number != number)
        {
            return;
        }

        _lobby = null;
        if (lobby.IsSimulated != dryRun.Enabled)
        {
            log.LogInformation("Boss lobby {Number} dropped: the dry run was switched", number);
            return;
        }

        var now = time.GetUtcNow();
        await using var db = database.Open();
        var participants = await FieldParticipantsAsync(db, lobby, ct);
        if (participants.Count < settings.MinParticipants)
        {
            chat.Send($"Der Boss entkommt – es haben sich zu wenige Trainer getraut. Mindestens {settings.MinParticipants} müssen mit !boss mitmachen.");
            if (!lobby.IsSimulated)
            {
                await SetCooldownAsync(db, now + settings.FailedLobbyCooldown, ct);
                await db.SaveChangesAsync(ct);
            }

            return;
        }

        await FightAsync(db, lobby, participants, now, ct);
    }

    /// <summary>Participants in join order, each with all their specimens; trainers gone meanwhile drop out.</summary>
    private static async Task<List<(Lobby.Member Member, List<Specimen> Specimens)>> FieldParticipantsAsync(
        PokeArenaDb db, Lobby lobby, CancellationToken ct)
    {
        var ids = lobby.Members.Select(m => m.UserId).ToList();
        var specimens = await db.Specimens.AsNoTracking().Where(s => ids.Contains(s.TrainerId)).OrderBy(s => s.Id).ToListAsync(ct);
        var byTrainer = specimens.GroupBy(s => s.TrainerId).ToDictionary(g => g.Key, g => g.ToList());
        return [.. lobby.Members.Where(m => byTrainer.ContainsKey(m.UserId)).Select(m => (m, byTrainer[m.UserId]))];
    }

    private async Task FightAsync(
        PokeArenaDb db,
        Lobby lobby,
        List<(Lobby.Member Member, List<Specimen> Specimens)> participants,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // Everything random — boss, the specimen each participant fields, the hits — comes from one seed.
        var seed = seeds.NextSeed();
        var random = new BattleRandom(seed);
        var n = participants.Count;
        var boss = BossDraw.Species(species, settings, n, random);
        var party = participants
            .Select(p => Fighter.Create(species.Get(random.Pick(p.Specimens).SpeciesId), battleSettings, p.Member.UserId, p.Member.DisplayName))
            .ToList();
        var result = new BossBattle(damage).Fight(BossDraw.Fighter(boss, battleSettings, settings, n), party, settings.TargetDivisor, random);
        if (result.HitRoundLimit)
        {
            log.LogWarning("Boss fight hit the round limit (seed {Seed}, boss {Boss}, {Count} participants)", seed, boss.Slug, n);
        }

        var catchers = participants.Where((_, i) => result.BossDefeated && result.Survived[i]).Select(p => p.Member).ToList();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var battle = new Battle
        {
            Kind = BattleKind.Boss,
            FoughtAt = now,
            Seed = seed,
            BossSpeciesId = boss.Id,
            Outcome = result.BossDefeated ? BattleOutcome.BossDefeated : BattleOutcome.BossEscaped,
            Rounds = result.Rounds,
            LogJson = result.Log.ToJson(),
            Participants =
            [
                .. participants.Select((p, i) => new BattleParticipant
                {
                    TrainerId = p.Member.UserId,
                    Won = result.BossDefeated && result.Survived[i],
                }),
            ],
        };
        db.Battles.Add(battle);
        db.Specimens.AddRange(catchers.Select(c => new Specimen
        {
            TrainerId = c.UserId,
            SpeciesId = boss.Id,
            CaughtAt = now,
            AcquiredAt = now,
            Origin = SpecimenOrigin.Boss,
        }));
        await db.SaveChangesAsync(ct);

        var achievements = await Achievements.UnlockNewAsync(db, [.. catchers.Select(c => c.UserId)], npcs, now, ct);
        await SiteOutbox.EnqueueAsync(db, SiteOutbox.ForBattle(battle.Id, participants.Select(p => p.Member.UserId)), now, ct);
        var cooldownUntil = now + settings.Cooldown;
        if (!lobby.IsSimulated)
        {
            await SetCooldownAsync(db, cooldownUntil, ct);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        if (!lobby.IsSimulated)
        {
            _ = AnnounceReadyAfterCooldownAsync(cooldownUntil, ct);
        }
        log.LogInformation(
            "Boss fight: {Boss} vs {Count} participants, {Outcome}, {Catches} catches, seed {Seed}",
            boss.Slug, n, result.BossDefeated ? "defeated" : "escaped", catchers.Count, seed);

        chat.Send($"Ein wildes {boss.Name}{BattleTexts.TierSuffix(boss)} erscheint! {n} Trainer kämpfen mit.");
        var outcome = result.BossDefeated
            ? $"{boss.Name} wurde besiegt! Gefangen von: {string.Join(", ", catchers.Select(c => c.DisplayName))}."
            : result.Survived.Any(s => s)
                ? $"{boss.Name} ist entkommen."
                : $"{boss.Name} ist entkommen – alle Pokémon wurden besiegt.";
        chat.Send(links.WithBattle(outcome, battle.Id, lobby.IsSimulated));
        foreach (var a in achievements)
        {
            chat.Send(Achievements.Announcement(a));
        }

        battles.NotifyChanged();
        if (catchers.Count > 0)
        {
            trainers.NotifyChanged();
        }
    }

    private async Task AnnounceReadyAfterCooldownAsync(DateTimeOffset until, CancellationToken ct)
    {
        try
        {
            await Task.Delay(until - time.GetUtcNow(), time, ct);
            inbox.Post(new QueuedWork("Boss bereit", workCt => AnnounceReadyAsync(until, workCt)));
        }
        catch (OperationCanceledException)
        {
            // App shutting down: nobody is left to hear it.
        }
    }

    /// <summary>
    /// Runs on the command queue once the cooldown ending at <paramref name="until"/> has elapsed. Stays silent if
    /// that cooldown is no longer the current one (<c>!bossreset</c> already said so, or a newer one replaced it),
    /// if a lobby is open, during the dry run, or while the bot takes no commands (stream offline).
    /// </summary>
    internal async Task AnnounceReadyAsync(DateTimeOffset until, CancellationToken ct)
    {
        if (dryRun.Enabled || !stream.AcceptsCommands || _lobby is { IsSimulated: false })
        {
            return;
        }

        if (await CooldownUntilAsync(ct) != until)
        {
            return;
        }

        chat.Send(ReadyAnnouncement);
    }

    /// <summary><c>!bossreset</c> (mods): the next <c>!boss</c> may open a lobby at once. An open lobby stays as it is.</summary>
    public async Task ResetCooldownAsync(CancellationToken ct)
    {
        await using var db = database.Open();
        await db.Cooldowns.Where(c => c.Key == CooldownKey).ExecuteDeleteAsync(ct);
        log.LogInformation("Boss cooldown reset");
        chat.Send("Der Boss-Cooldown ist zurückgesetzt – mit !boss geht es sofort weiter.");
    }

    private async Task<DateTimeOffset?> CooldownUntilAsync(CancellationToken ct)
    {
        await using var db = database.Open();
        return await db.Cooldowns.Where(c => c.Key == CooldownKey).Select(c => (DateTimeOffset?)c.Until).FirstOrDefaultAsync(ct);
    }

    private static async Task SetCooldownAsync(PokeArenaDb db, DateTimeOffset until, CancellationToken ct)
    {
        var cooldown = await db.Cooldowns.FindAsync([CooldownKey], ct);
        if (cooldown is null)
        {
            db.Cooldowns.Add(new Cooldown { Key = CooldownKey, Until = until });
        }
        else
        {
            cooldown.Until = until;
        }
    }

    private void ReplyCooldown(ChatMessage message, TimeSpan remaining, DateTimeOffset now)
    {
        if (_lastCooldownReply is { } last && now - last < CooldownReplyInterval)
        {
            return;
        }

        _lastCooldownReply = now;
        var minutes = (int)Math.Ceiling(remaining.TotalMinutes);
        var when = minutes == 1 ? "in 1 Minute" : $"in {minutes} Minuten";
        chat.Send($"@{message.DisplayName}, der nächste Boss kann {when} gerufen werden.");
    }

    private sealed class Lobby(long number, bool isSimulated)
    {
        private readonly List<Member> _members = [];

        public long Number => number;

        /// <summary>Opened from the dry run; only simulated messages may join, only the sandbox gets the result.</summary>
        public bool IsSimulated => isSimulated;

        /// <summary>In join order; the initiator first.</summary>
        public IReadOnlyList<Member> Members => _members;

        /// <summary>Joining twice is a no-op (spec §4).</summary>
        public void Join(string userId, string displayName)
        {
            if (_members.All(m => m.UserId != userId))
            {
                _members.Add(new Member(userId, displayName));
            }
        }

        public sealed record Member(string UserId, string DisplayName);
    }
}
