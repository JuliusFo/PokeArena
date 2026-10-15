using Microsoft.EntityFrameworkCore;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;

namespace PokeArena.Core.Game;

/// <summary>A row of the battle list in the desktop app.</summary>
/// <param name="Boss">Boss fights: the boss species.</param>
/// <param name="Winners">Boss fights: participants who caught the boss.</param>
public sealed record BattleSummary(long Id, BattleKind Kind, DateTimeOffset FoughtAt, Species? Boss, BattleOutcome Outcome, int Participants, int Winners);

/// <summary>Reads stored battles for the desktop app. Game logic writes them and calls <see cref="NotifyChanged"/>.</summary>
public sealed class BattleService
{
    private readonly GameDatabase _database;
    private readonly SpeciesCatalog _species;

    public BattleService(GameDatabase database, SpeciesCatalog species)
    {
        _database = database;
        _species = species;
        _database.Switched += NotifyChanged;
    }

    /// <summary>Raised after a battle was stored or the database was switched.</summary>
    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();

    /// <summary>The latest battles, newest first.</summary>
    public async Task<IReadOnlyList<BattleSummary>> ListAsync(int limit, CancellationToken ct)
    {
        await using var db = _database.Open();
        var rows = await db.Battles
            .OrderByDescending(b => b.FoughtAt)
            .ThenByDescending(b => b.Id)
            .Take(limit)
            .Select(b => new
            {
                b.Id,
                b.Kind,
                b.FoughtAt,
                b.BossSpeciesId,
                b.Outcome,
                Participants = b.Participants.Count,
                Winners = b.Participants.Count(p => p.Won),
            })
            .ToListAsync(ct);

        return [.. rows.Select(r => new BattleSummary(
            r.Id,
            r.Kind,
            r.FoughtAt,
            r.BossSpeciesId is { } id ? _species.Get(id) : null,
            r.Outcome,
            r.Participants,
            r.Winners))];
    }

    /// <summary>The German log lines of a battle, or null if it does not exist (any more).</summary>
    public async Task<IReadOnlyList<string>?> LinesAsync(long battleId, CancellationToken ct)
    {
        await using var db = _database.Open();
        var json = await db.Battles.Where(b => b.Id == battleId).Select(b => b.LogJson).FirstOrDefaultAsync(ct);
        return json is null ? null : BattleLog.FromJson(json).ToLines(_species);
    }
}
