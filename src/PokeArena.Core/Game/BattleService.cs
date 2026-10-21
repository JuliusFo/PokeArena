using Microsoft.EntityFrameworkCore;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;

namespace PokeArena.Core.Game;

/// <summary>A row of the battle list in the desktop app.</summary>
/// <param name="Boss">Boss fights: the boss species.</param>
/// <param name="Winners">Boss fights: participants who caught the boss. PvP: 1.</param>
/// <param name="Winner">PvP and NPC: display name of the winning trainer; null if gone or the NPC won.</param>
/// <param name="Loser">PvP and NPC: display name of the losing trainer; null if gone or the trainer won.</param>
/// <param name="Loot">PvP: species the winner took. NPC: reward species of a first win.</param>
/// <param name="Npc">NPC fights: the NPC.</param>
/// <param name="Tokens">NPC wins: tokens credited.</param>
public sealed record BattleSummary(
    long Id,
    BattleKind Kind,
    DateTimeOffset FoughtAt,
    Species? Boss,
    BattleOutcome Outcome,
    int Participants,
    int Winners,
    string? Winner = null,
    string? Loser = null,
    Species? Loot = null,
    Npc? Npc = null,
    int? Tokens = null);

/// <summary>Reads stored battles for the desktop app. Game logic writes them and calls <see cref="NotifyChanged"/>.</summary>
public sealed class BattleService
{
    private readonly GameDatabase _database;
    private readonly SpeciesCatalog _species;
    private readonly NpcCatalog _npcs;

    public BattleService(GameDatabase database, SpeciesCatalog species, NpcCatalog npcs)
    {
        _database = database;
        _species = species;
        _npcs = npcs;
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
                b.LootSpeciesId,
                b.NpcId,
                b.Tokens,
                Participants = b.Participants.Count,
                Winners = b.Participants.Count(p => p.Won),
                Winner = b.Participants.Where(p => p.Won).Select(p => p.Trainer!.DisplayName).FirstOrDefault(),
                Loser = b.Participants.Where(p => !p.Won).Select(p => p.Trainer!.DisplayName).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return [.. rows.Select(r => new BattleSummary(
            r.Id,
            r.Kind,
            r.FoughtAt,
            r.BossSpeciesId is { } id ? _species.Get(id) : null,
            r.Outcome,
            r.Participants,
            r.Winners,
            r.Kind == BattleKind.Boss ? null : r.Winner,
            r.Kind == BattleKind.Boss ? null : r.Loser,
            r.LootSpeciesId is { } loot ? _species.Get(loot) : null,
            r.NpcId is { } npc ? _npcs.Find(npc) : null,
            r.Tokens))];
    }

    /// <summary>The German log lines of a battle, or null if it does not exist (any more).</summary>
    public async Task<IReadOnlyList<string>?> LinesAsync(long battleId, CancellationToken ct)
    {
        await using var db = _database.Open();
        var json = await db.Battles.Where(b => b.Id == battleId).Select(b => b.LogJson).FirstOrDefaultAsync(ct);
        return json is null ? null : BattleLog.FromJson(json).ToLines(_species);
    }
}
