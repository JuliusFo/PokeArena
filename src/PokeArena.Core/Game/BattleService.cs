using Microsoft.EntityFrameworkCore;
using PokeArena.Contracts;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;

namespace PokeArena.Core.Game;

/// <summary>A row of the battle list in the desktop app: the same headline and result as on the website.</summary>
/// <param name="Participants">Trainers who took part.</param>
public sealed record BattleListRow(long Id, BattleKind Kind, DateTimeOffset FoughtAt, int Participants, string Headline, string Result);

/// <summary>Reads stored battles for the desktop app. Game logic writes them and calls <see cref="NotifyChanged"/>.</summary>
public sealed class BattleService
{
    private readonly GameDatabase _database;
    private readonly SpeciesCatalog _species;
    private readonly BattleTexts _texts;

    public BattleService(GameDatabase database, SpeciesCatalog species, NpcCatalog npcs)
    {
        _database = database;
        _species = species;
        _texts = new BattleTexts(species, npcs);
        _database.Switched += NotifyChanged;
    }

    /// <summary>Raised after a battle was stored or the database was switched.</summary>
    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();

    /// <summary>The latest battles, newest first.</summary>
    public async Task<IReadOnlyList<BattleListRow>> ListAsync(int limit, CancellationToken ct)
    {
        await using var db = _database.Open();
        var battles = await db.Battles.AsNoTracking()
            .Include(b => b.Participants)
            .OrderByDescending(b => b.FoughtAt)
            .ThenByDescending(b => b.Id)
            .Take(limit)
            .ToListAsync(ct);

        return [.. battles.Select(b =>
        {
            var texts = _texts.Describe(b, BattleLog.FromJson(b.LogJson));
            return new BattleListRow(b.Id, b.Kind, b.FoughtAt, b.Participants.Count, texts.Headline, texts.Result);
        })];
    }

    /// <summary>The German log lines of a battle, or null if it does not exist (any more).</summary>
    public async Task<IReadOnlyList<string>?> LinesAsync(long battleId, CancellationToken ct)
    {
        await using var db = _database.Open();
        var json = await db.Battles.Where(b => b.Id == battleId).Select(b => b.LogJson).FirstOrDefaultAsync(ct);
        return json is null ? null : BattleLog.FromJson(json).ToLines(_species);
    }
}
