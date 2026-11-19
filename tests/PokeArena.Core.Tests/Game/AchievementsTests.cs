using PokeArena.Core.Data;
using PokeArena.Core.Game;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Game;

public sealed class AchievementsTests : IDisposable
{
    private readonly GameFixture _game = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_threshold_is_reached_with_exactly_its_count_not_one_species_early()
    {
        // Achievements are never revoked: unlocking Eich at 14 species would be permanent (review 1.8.1, C7).
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", GameFixture.Species.Starters[0], _ct);
        await GiveDistinctSpeciesAsync(14);
        Assert.Empty(await UnlockAsync());

        await GiveDistinctSpeciesAsync(15);
        Assert.Equal([15], (await UnlockAsync()).Select(a => a.Npc.Threshold));
    }

    private async Task<IReadOnlyList<NewAchievement>> UnlockAsync()
    {
        await using var db = _game.OpenLive();
        var reached = await Achievements.UnlockNewAsync(db, ["1"], GameFixture.Npcs, _game.Time.GetUtcNow(), _ct);
        await db.SaveChangesAsync(_ct);
        return reached;
    }

    /// <summary>Tops trainer 1 up to exactly <paramref name="count"/> distinct species.</summary>
    private async Task GiveDistinctSpeciesAsync(int count)
    {
        await using var db = _game.OpenLive();
        var owned = db.Specimens.Where(s => s.TrainerId == "1").Select(s => s.SpeciesId).Distinct().ToList();
        db.Specimens.AddRange(GameFixture.Species.All
            .Where(s => !owned.Contains(s.Id))
            .Take(count - owned.Count)
            .Select(s => new Specimen { TrainerId = "1", SpeciesId = s.Id, Origin = SpecimenOrigin.Boss }));
        await db.SaveChangesAsync(_ct);
    }

    public void Dispose() => _game.Dispose();
}
