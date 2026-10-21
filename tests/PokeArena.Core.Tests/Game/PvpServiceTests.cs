using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PokeArena.Core.Chat;
using PokeArena.Core.Data;
using PokeArena.Core.Game;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Game;

public class PvpServiceTests : IDisposable
{
    private const int Seed = 4321;

    /// <summary>
    /// Every hit knocks out: with equal team sizes the challenger always wins, because it strikes first and the
    /// sides then trade one knockout each.
    /// </summary>
    private static readonly BattleSettings OneHitKnockouts = BattleSettings.Default with { Scaling = 100_000 };

    private readonly GameFixture _game = new();
    private readonly RecordingSender _chat = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static SpeciesCatalog Species => GameFixture.Species;

    private static Species Starter => Species.Starters[0];

    [Fact]
    public async Task Accepted_challenge_resolves_at_once_and_the_winner_takes_a_specimen_of_the_loser()
    {
        var pvp = Pvp(OneHitKnockouts);
        await RegisterAsync("1", "Ash", "Mewtu", 5);
        await RegisterAsync("2", "Misty", "Karpador", 5);

        await pvp.FightAsync(Fight("1", "Ash"), "@misty", _ct);
        Assert.Equal(["@Misty, Ash fordert dich zum Kampf heraus! Nimm an mit !fight @ash – du hast 2 Minuten."], _chat.Sent);

        await pvp.FightAsync(Fight("2", "Misty"), "@Ash", _ct);

        Assert.Equal("Ash besiegt Misty und bekommt Karpador von Misty!", _chat.Sent[^1]);
        await using var db = _game.OpenLive();
        var battle = await db.Battles.Include(b => b.Participants).SingleAsync(_ct);
        Assert.Equal((BattleKind.Pvp, BattleOutcome.ChallengerWon, Seed), (battle.Kind, battle.Outcome, battle.Seed));
        Assert.Equal([("1", true), ("2", false)], battle.Participants.OrderBy(p => p.TrainerId).Select(p => (p.TrainerId, p.Won)));
        Assert.Equal(12, BattleLog.FromJson(battle.LogJson).Fighters.Count);

        var loot = await db.Specimens.SingleAsync(s => s.Id == battle.LootSpecimenId, _ct);
        Assert.Equal(("1", Species.Find("Karpador")!.Id, false), (loot.TrainerId, loot.SpeciesId, loot.IsStarter));
        Assert.Equal(loot.SpeciesId, battle.LootSpeciesId);
        Assert.Equal(7, await db.Specimens.CountAsync(s => s.TrainerId == "1", _ct));
        Assert.Equal(5, await db.Specimens.CountAsync(s => s.TrainerId == "2", _ct));
    }

    [Fact]
    public async Task The_challenged_trainer_can_win_too()
    {
        var pvp = Pvp(BattleSettings.Default);
        await RegisterAsync("1", "Ash", "Karpador", 5);
        await RegisterAsync("2", "Misty", "Mewtu", 5);

        await pvp.FightAsync(Fight("1", "Ash"), "@misty", _ct);
        await pvp.FightAsync(Fight("2", "Misty"), "@ash", _ct);

        Assert.Equal("Misty besiegt Ash und bekommt Karpador von Ash!", _chat.Sent[^1]);
        await using var db = _game.OpenLive();
        Assert.Equal(BattleOutcome.OpponentWon, (await db.Battles.SingleAsync(_ct)).Outcome);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task The_starter_is_never_the_loot(int seed)
    {
        // The loser owns exactly six specimens, so the starter is always in the team.
        var pvp = Pvp(OneHitKnockouts, seed);
        await RegisterAsync("1", "Ash", "Mewtu", 5);
        await RegisterAsync("2", "Misty", "Karpador", 5);

        await pvp.FightAsync(Fight("1", "Ash"), "@misty", _ct);
        await pvp.FightAsync(Fight("2", "Misty"), "@ash", _ct);

        await using var db = _game.OpenLive();
        Assert.Equal(Species.Find("Karpador")!.Id, (await db.Battles.SingleAsync(_ct)).LootSpeciesId);
        Assert.True(await db.Specimens.AnyAsync(s => s.IsStarter && s.TrainerId == "2", _ct));
    }

    [Fact]
    public async Task Invalid_challenges_get_a_hint()
    {
        var pvp = Pvp(OneHitKnockouts);
        await RegisterAsync("1", "Ash", "Mewtu", 5);
        await RegisterAsync("2", "Misty", "Karpador", 2);

        await pvp.FightAsync(Fight("1", "Ash"), "", _ct);
        await pvp.FightAsync(Fight("1", "Ash"), "@", _ct);
        await pvp.FightAsync(Fight("1", "Ash"), "@gary", _ct);
        await pvp.FightAsync(Fight("1", "Ash"), "@ASH", _ct);
        await pvp.FightAsync(Fight("1", "Ash"), "@misty", _ct);
        await pvp.FightAsync(Fight("2", "Misty"), "@ash", _ct);

        Assert.Equal(
        [
            "@Ash, fordere jemanden zum Kampf heraus – zum Beispiel: !fight @Name",
            "@Ash, fordere jemanden zum Kampf heraus – zum Beispiel: !fight @Name",
            "@Ash, gary ist noch kein Trainer.",
            "@Ash, du kannst dich nicht selbst herausfordern.",
            "@Ash, Misty hat erst 3 Pokémon – für einen Kampf braucht ihr beide mindestens 6.",
            "@Misty, du hast erst 3 Pokémon – für einen Kampf braucht ihr beide mindestens 6.",
        ], _chat.Sent);
    }

    [Fact]
    public async Task A_trainer_has_at_most_one_open_challenge_in_either_direction()
    {
        var pvp = Pvp(OneHitKnockouts);
        await RegisterAsync("1", "Ash", "Mewtu", 5);
        await RegisterAsync("2", "Misty", "Karpador", 5);
        await RegisterAsync("3", "Rocko", "Onix", 5);

        await pvp.FightAsync(Fight("1", "Ash"), "@misty", _ct);
        await pvp.FightAsync(Fight("1", "Ash"), "@rocko", _ct);
        await pvp.FightAsync(Fight("1", "Ash"), "@misty", _ct);
        await pvp.FightAsync(Fight("2", "Misty"), "@rocko", _ct);
        await pvp.FightAsync(Fight("3", "Rocko"), "@misty", _ct);

        Assert.Equal(
        [
            "@Ash, du hast schon eine offene Herausforderung.",
            "@Ash, du hast schon eine offene Herausforderung.",
            "@Misty, du hast schon eine offene Herausforderung.",
            "@Rocko, Misty hat schon eine offene Herausforderung.",
        ], _chat.Sent.Skip(1));
    }

    [Fact]
    public async Task An_unanswered_challenge_expires_silently()
    {
        var pvp = Pvp(OneHitKnockouts);
        await RegisterAsync("1", "Ash", "Mewtu", 5);
        await RegisterAsync("2", "Misty", "Karpador", 5);
        await pvp.FightAsync(Fight("1", "Ash"), "@misty", _ct);

        _game.Time.Advance(PvpSettings.Default.ChallengeTimeout);
        await pvp.FightAsync(Fight("2", "Misty"), "@ash", _ct);

        Assert.Equal("@Ash, Misty fordert dich zum Kampf heraus! Nimm an mit !fight @misty – du hast 2 Minuten.", _chat.Sent[^1]);
        Assert.Equal(2, _chat.Sent.Count);
        await using var db = _game.OpenLive();
        Assert.Empty(db.Battles);
    }

    [Fact]
    public async Task Accepting_checks_the_team_size_again()
    {
        var pvp = Pvp(OneHitKnockouts);
        await RegisterAsync("1", "Ash", "Mewtu", 5);
        await RegisterAsync("2", "Misty", "Karpador", 5);
        await pvp.FightAsync(Fight("1", "Ash"), "@misty", _ct);
        await using (var db = _game.OpenLive())
        {
            db.Specimens.Remove(await db.Specimens.FirstAsync(s => s.TrainerId == "1" && !s.IsStarter, _ct));
            await db.SaveChangesAsync(_ct);
        }

        await pvp.FightAsync(Fight("2", "Misty"), "@ash", _ct);

        Assert.Equal("Der Kampf fällt aus: Ash hat erst 5 Pokémon – für einen Kampf braucht ihr beide mindestens 6.", _chat.Sent[^1]);
        await using var read = _game.OpenLive();
        Assert.Empty(read.Battles);
    }

    [Fact]
    public async Task Loot_that_reaches_a_threshold_unlocks_the_achievement()
    {
        var pvp = Pvp(OneHitKnockouts);
        await RegisterAsync("1", "Ash", "Mewtu", 5);
        await RegisterAsync("2", "Misty", "Karpador", 5);
        await GiveDistinctSpeciesAsync("1", 14, except: Species.Find("Karpador")!);

        await pvp.FightAsync(Fight("1", "Ash"), "@misty", _ct);
        await pvp.FightAsync(Fight("2", "Misty"), "@ash", _ct);

        Assert.Equal("Ash hat AV 15 freigeschalten und kann NPC Eich herausfordern.", _chat.Sent[^1]);
        await using var db = _game.OpenLive();
        Assert.Equal(("1", 15), await db.AchievementUnlocks.Select(u => ValueTuple.Create(u.TrainerId, u.Threshold)).SingleAsync(_ct));
    }

    [Fact]
    public async Task Switching_the_database_drops_open_challenges()
    {
        var pvp = Pvp(OneHitKnockouts);
        await RegisterAsync("1", "Ash", "Mewtu", 5);
        await RegisterAsync("2", "Misty", "Karpador", 5);
        await pvp.FightAsync(Fight("1", "Ash"), "@misty", _ct);

        _game.DryRun.Enabled = true;
        _game.DryRun.Enabled = false;
        await pvp.FightAsync(Fight("2", "Misty"), "@ash", _ct);

        Assert.StartsWith("@Ash, Misty fordert dich zum Kampf heraus!", _chat.Sent[^1]);
        await using var db = _game.OpenLive();
        Assert.Empty(db.Battles);
    }

    private PvpService Pvp(BattleSettings battleSettings, int seed = Seed) => new(
        _game.Database,
        Species,
        GameFixture.Npcs,
        battleSettings,
        PvpSettings.Default,
        new DamageCalculator(battleSettings, GameFixture.Types),
        new FixedSeeds(seed),
        _chat,
        _game.Trainers,
        new BattleService(_game.Database, Species, GameFixture.Npcs),
        _game.Time,
        NullLogger<PvpService>.Instance);

    /// <summary>Registers a trainer with the first starter plus <paramref name="count"/> specimens of one species.</summary>
    private async Task RegisterAsync(string id, string name, string species, int count)
    {
        await _game.Trainers.RegisterAsync(id, name.ToLowerInvariant(), name, Starter, _ct);
        await using var db = _game.Database.Open();
        var speciesId = Species.Find(species)!.Id;
        db.Specimens.AddRange(Enumerable.Range(0, count).Select(_ => new Specimen { TrainerId = id, SpeciesId = speciesId, Origin = SpecimenOrigin.Boss }));
        await db.SaveChangesAsync(_ct);
    }

    /// <summary>Tops the trainer up to <paramref name="count"/> distinct species, none of them <paramref name="except"/>.</summary>
    private async Task GiveDistinctSpeciesAsync(string trainerId, int count, Species except)
    {
        await using var db = _game.Database.Open();
        var owned = await db.Specimens.Where(s => s.TrainerId == trainerId).Select(s => s.SpeciesId).Distinct().ToListAsync(_ct);
        db.Specimens.AddRange(Species.All
            .Where(s => s.Id != except.Id && !owned.Contains(s.Id))
            .Take(count - owned.Count)
            .Select(s => new Specimen { TrainerId = trainerId, SpeciesId = s.Id, Origin = SpecimenOrigin.Boss }));
        await db.SaveChangesAsync(_ct);
    }

    private static ChatMessage Fight(string userId, string name) =>
        new(Guid.NewGuid().ToString(), userId, name.ToLowerInvariant(), name, "!fight", ChatRole.Viewer);

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
