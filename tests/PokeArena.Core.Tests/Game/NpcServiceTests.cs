using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PokeArena.Core.Chat;
using PokeArena.Core.Data;
using PokeArena.Core.Game;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Game;

public class NpcServiceTests : IDisposable
{
    private const int Seed = 4321;

    /// <summary>
    /// Every hit knocks out: the trainer strikes first, so with six against six the trainer always wins and with
    /// five against six the NPC always wins.
    /// </summary>
    private static readonly BattleSettings OneHitKnockouts = BattleSettings.Default with { Scaling = 100_000 };

    private static readonly PvpSettings TeamOfFive = PvpSettings.Default with { TeamSize = 5 };

    private readonly GameFixture _game = new();
    private readonly RecordingSender _chat = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static SpeciesCatalog Species => GameFixture.Species;

    private static Npc Eich => GameFixture.Npcs.Find("eich")!;

    [Fact]
    public async Task The_first_win_brings_badge_tokens_and_a_species_of_the_npc_team_the_trainer_lacks()
    {
        await RegisterAsync("1", "Ash", "Karpador", 5, unlock: 15);

        await Npc().ChallengeAsync(Challenge("1", "Ash"), "eich", _ct);

        await using var db = _game.OpenLive();
        var battle = await db.Battles.Include(b => b.Participants).SingleAsync(_ct);
        Assert.Equal((BattleKind.Npc, "eich", BattleOutcome.ChallengerWon, 1, Seed), (battle.Kind, battle.NpcId, battle.Outcome, battle.Tokens, battle.Seed));
        Assert.Equal([("1", true)], battle.Participants.Select(p => (p.TrainerId, p.Won)));
        Assert.Equal(12, BattleLog.FromJson(battle.LogJson).Fighters.Count);

        var reward = await db.Specimens.SingleAsync(s => s.Id == battle.LootSpecimenId, _ct);
        Assert.Equal(("1", SpecimenOrigin.Npc, reward.SpeciesId), (reward.TrainerId, reward.Origin, battle.LootSpeciesId!.Value));
        Assert.Contains(Eich.Team, s => s.Id == reward.SpeciesId);
        Assert.NotEqual(Species.Starters[0].Id, reward.SpeciesId);

        Assert.Equal(1, (await db.Trainers.SingleAsync(_ct)).Tokens);
        var badge = await db.NpcBadges.SingleAsync(_ct);
        Assert.Equal(("1", "eich", battle.Id, _game.Time.GetUtcNow()), (badge.TrainerId, badge.NpcId, badge.BattleId, badge.WonAt));
        Assert.Equal(_game.Time.GetUtcNow() + NpcSettings.Default.WinCooldown, (await db.Cooldowns.SingleAsync(_ct)).Until);
        Assert.Equal([$"Ash besiegt Eich und bekommt den Orden von Eich, {Species.Get(reward.SpeciesId).Name} und 1 Token!"], _chat.Sent);
    }

    [Fact]
    public async Task Further_wins_bring_only_the_tokens()
    {
        await RegisterAsync("1", "Ash", "Karpador", 5, unlock: 15);
        var npc = Npc();
        await npc.ChallengeAsync(Challenge("1", "Ash"), "Eich", _ct);

        _game.Time.Advance(NpcSettings.Default.WinCooldown);
        await npc.ChallengeAsync(Challenge("1", "Ash"), "EICH", _ct);

        Assert.Equal("Ash besiegt Eich erneut und bekommt 1 Token!", _chat.Sent[^1]);
        await using var db = _game.OpenLive();
        Assert.Equal(2, await db.Battles.CountAsync(_ct));
        Assert.Null((await db.Battles.OrderBy(b => b.Id).LastAsync(_ct)).LootSpeciesId);
        Assert.Equal(7, await db.Specimens.CountAsync(_ct));
        Assert.Equal(2, (await db.Trainers.SingleAsync(_ct)).Tokens);
        Assert.Single(db.NpcBadges);
    }

    [Fact]
    public async Task A_loss_brings_nothing_and_the_long_cooldown()
    {
        await RegisterAsync("1", "Ash", "Karpador", 5, unlock: 15);

        await Npc(TeamOfFive).ChallengeAsync(Challenge("1", "Ash"), "eich", _ct);

        Assert.Equal(["Eich besiegt Ash! Nächster Versuch in 48 Stunden."], _chat.Sent);
        await using var db = _game.OpenLive();
        var battle = await db.Battles.Include(b => b.Participants).SingleAsync(_ct);
        Assert.Equal((BattleOutcome.OpponentWon, (int?)null, (int?)null, false), (battle.Outcome, battle.Tokens, battle.LootSpeciesId, battle.Participants.Single().Won));
        Assert.Equal(0, (await db.Trainers.SingleAsync(_ct)).Tokens);
        Assert.Empty(db.NpcBadges);
        Assert.Equal(6, await db.Specimens.CountAsync(_ct));
        Assert.Equal(_game.Time.GetUtcNow() + NpcSettings.Default.LossCooldown, (await db.Cooldowns.SingleAsync(_ct)).Until);
    }

    [Fact]
    public async Task The_cooldown_is_per_npc_and_survives_a_restart()
    {
        await RegisterAsync("1", "Ash", "Karpador", 30, unlock: 25);
        await using (var db = _game.OpenLive())
        {
            db.AchievementUnlocks.Add(new AchievementUnlock { TrainerId = "1", Threshold = 15 });
            await db.SaveChangesAsync(_ct);
        }

        await Npc(TeamOfFive).ChallengeAsync(Challenge("1", "Ash"), "eich", _ct);
        _game.Time.Advance(TimeSpan.FromMinutes(90.5));

        var afterRestart = Npc(TeamOfFive, _game.NewDatabase());
        await afterRestart.ChallengeAsync(Challenge("1", "Ash"), "eich", _ct);
        await afterRestart.ChallengeAsync(Challenge("1", "Ash"), "rocko", _ct);

        Assert.Equal("@Ash, du kannst Eich noch nicht herausfordern (Cooldown 46:30).", _chat.Sent[1]);
        Assert.Equal("Rocko besiegt Ash! Nächster Versuch in 48 Stunden.", _chat.Sent[2]);
    }

    [Fact]
    public async Task The_dry_run_skips_the_cooldown()
    {
        await RegisterAsync("1", "Ash", "Karpador", 5, unlock: 15);
        var npc = Npc();

        await npc.ChallengeAsync(Challenge("1", "Ash", simulated: true), "eich", _ct);
        await npc.ChallengeAsync(Challenge("1", "Ash", simulated: true), "eich", _ct);

        Assert.Equal("Ash besiegt Eich erneut und bekommt 1 Token!", _chat.Sent[^1]);
        await using var db = _game.OpenLive();
        Assert.Empty(db.Cooldowns);
    }

    [Fact]
    public async Task Locked_unknown_and_missing_npcs_get_a_hint()
    {
        await RegisterAsync("1", "Ash", "Karpador", 5);
        await RegisterAsync("2", "Misty", "Karpador", 5, unlock: 15);
        var npc = Npc();

        await npc.ChallengeAsync(Challenge("1", "Ash"), "", _ct);
        await npc.ChallengeAsync(Challenge("1", "Ash"), "Eich", _ct);
        await npc.ChallengeAsync(Challenge("2", "Misty"), "", _ct);
        await npc.ChallengeAsync(Challenge("2", "Misty"), "Gary", _ct);
        await npc.ChallengeAsync(Challenge("2", "Misty"), "Rocko", _ct);

        Assert.Equal(
        [
            "@Ash, fordere einen NPC heraus. Den ersten (Eich) schaltest du mit 15 verschiedenen Pokémon frei.",
            "@Ash, du kannst Eich noch nicht herausfordern (noch 13 Pokémon benötigt).",
            "@Misty, fordere einen NPC heraus – zum Beispiel: !challenge Eich. Freigeschaltet: Eich.",
            "@Misty, einen NPC namens Gary gibt es nicht – zum Beispiel: !challenge Eich. Freigeschaltet: Eich.",
            "@Misty, du kannst Rocko noch nicht herausfordern (noch 23 Pokémon benötigt).",
        ], _chat.Sent);
        await using var db = _game.OpenLive();
        Assert.Empty(db.Battles);
    }

    [Fact]
    public async Task A_trainer_needs_a_full_team()
    {
        // The achievement stays even after the specimens are gone (e.g. lost in PvP).
        await RegisterAsync("1", "Ash", "Karpador", 4, unlock: 15);

        await Npc().ChallengeAsync(Challenge("1", "Ash"), "eich", _ct);

        Assert.Equal(["@Ash, du hast erst 5 Pokémon – für einen Kampf brauchst du mindestens 6."], _chat.Sent);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task A_trainer_who_owns_the_whole_team_gets_one_of_it_anyway(int seed)
    {
        await RegisterAsync("1", "Ash", "Karpador", 0, unlock: 15);
        await using (var db = _game.OpenLive())
        {
            db.Specimens.AddRange(Eich.Team.Select(s => new Specimen { TrainerId = "1", SpeciesId = s.Id, Origin = SpecimenOrigin.Boss }));
            await db.SaveChangesAsync(_ct);
        }

        await Npc(seed: seed).ChallengeAsync(Challenge("1", "Ash"), "eich", _ct);

        await using var read = _game.OpenLive();
        var battle = await read.Battles.SingleAsync(_ct);
        Assert.Contains(Eich.Team, s => s.Id == battle.LootSpeciesId);
    }

    [Fact]
    public async Task A_reward_that_reaches_a_threshold_unlocks_the_achievement()
    {
        await RegisterAsync("1", "Ash", "Karpador", 5, unlock: 15);
        await GiveDistinctSpeciesAsync("1", 24, except: Eich.Team);

        await Npc().ChallengeAsync(Challenge("1", "Ash"), "eich", _ct);

        Assert.Equal("Ash hat AV 25 freigeschalten und kann NPC Rocko herausfordern.", _chat.Sent[^1]);
        await using var db = _game.OpenLive();
        Assert.True(await db.AchievementUnlocks.AnyAsync(u => u.TrainerId == "1" && u.Threshold == 25, _ct));
    }

    private NpcService Npc(PvpSettings? pvpSettings = null, GameDatabase? database = null, int seed = Seed) => new(
        database ?? _game.Database,
        Species,
        GameFixture.Npcs,
        OneHitKnockouts,
        pvpSettings ?? PvpSettings.Default,
        NpcSettings.Default,
        new DamageCalculator(OneHitKnockouts, GameFixture.Types),
        new FixedSeeds(seed),
        _chat,
        _game.Trainers,
        new BattleService(database ?? _game.Database, Species, GameFixture.Npcs),
        _game.Time,
        NullLogger<NpcService>.Instance);

    /// <summary>
    /// Registers a trainer with the first starter plus <paramref name="count"/> specimens of one species, and
    /// optionally gives them the achievement at <paramref name="unlock"/>.
    /// </summary>
    private async Task RegisterAsync(string id, string name, string species, int count, int? unlock = null)
    {
        await _game.Trainers.RegisterAsync(id, name.ToLowerInvariant(), name, Species.Starters[0], _ct);
        await using var db = _game.Database.Open();
        var speciesId = Species.Find(species)!.Id;
        db.Specimens.AddRange(Enumerable.Range(0, count).Select(_ => new Specimen { TrainerId = id, SpeciesId = speciesId, Origin = SpecimenOrigin.Boss }));
        if (unlock is { } threshold)
        {
            db.AchievementUnlocks.Add(new AchievementUnlock { TrainerId = id, Threshold = threshold });
        }

        await db.SaveChangesAsync(_ct);
    }

    /// <summary>Tops the trainer up to <paramref name="count"/> distinct species, none of them in <paramref name="except"/>.</summary>
    private async Task GiveDistinctSpeciesAsync(string trainerId, int count, IReadOnlyList<Species> except)
    {
        await using var db = _game.Database.Open();
        var owned = await db.Specimens.Where(s => s.TrainerId == trainerId).Select(s => s.SpeciesId).Distinct().ToListAsync(_ct);
        db.Specimens.AddRange(Species.All
            .Where(s => except.All(e => e.Id != s.Id) && !owned.Contains(s.Id))
            .Take(count - owned.Count)
            .Select(s => new Specimen { TrainerId = trainerId, SpeciesId = s.Id, Origin = SpecimenOrigin.Boss }));
        await db.SaveChangesAsync(_ct);
    }

    private static ChatMessage Challenge(string userId, string name, bool simulated = false) =>
        new(Guid.NewGuid().ToString(), userId, name.ToLowerInvariant(), name, "!challenge", ChatRole.Viewer, simulated);

    public void Dispose() => _game.Dispose();

    private sealed class FixedSeeds(int seed) : ISeedSource
    {
        public int NextSeed() => seed;
    }

    private sealed class RecordingSender : IChatSender
    {
        public List<string> Sent { get; } = [];

        public void Send(string text) => Sent.Add(text);
    }
}
