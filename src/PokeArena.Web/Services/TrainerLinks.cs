using PokeArena.Contracts;

namespace PokeArena.Web.Services;

/// <summary>
/// Links to a trainer page, one way for every page: leaderboard, statistics and the fight page with its cards and its
/// „Schaden“ box (claude/kampf-trainer-link-3fc5e0).
/// </summary>
public static class TrainerLinks
{
    /// <summary>
    /// The trainer page of <paramref name="trainerId"/>, relative like every link of the site and escaped; null without
    /// an id (a deleted trainer, a boss, an NPC — or an empty id from a file no bot writes), so the name stays text.
    /// </summary>
    public static string? Href(string? trainerId) =>
        trainerId is { Length: > 0 } ? $"trainer/{Uri.EscapeDataString(trainerId)}" : null;

    /// <summary>
    /// The trainer whose name heads side <paramref name="side"/> of <paramref name="battle"/>: every fighter carries
    /// that name as owner and one trainer id. Null in a boss fight — side 0 is the group „Trainer“ even when a viewer
    /// of that name fights alone (each row links its own owner), side 1 the boss — and for an NPC and a deleted
    /// trainer (no id; never tell them apart by the missing id alone).
    /// </summary>
    public static TrainerRef? OfSide(BattlePage battle, int side)
    {
        if (battle.Kind == BattleKind.Boss || side < 0 || side >= battle.Sides.Count)
        {
            return null;
        }

        var fighters = battle.Sides[side].Fighters;
        var name = battle.Sides[side].Name;
        return fighters.Count > 0 && Href(fighters[0].TrainerId) is not null &&
            fighters.All(f => f.TrainerId == fighters[0].TrainerId && f.Owner == name)
                ? new TrainerRef(fighters[0].TrainerId!, name, fighters[0].Color)
                : null;
    }
}
