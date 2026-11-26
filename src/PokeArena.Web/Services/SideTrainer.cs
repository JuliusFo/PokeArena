using PokeArena.Contracts;

namespace PokeArena.Web.Services;

/// <summary>
/// The trainer a fight card stands for, so its heading and its „Schaden“ bar link to the trainer page like the
/// leaderboard and the statistics (claude/kampf-trainer-link-3fc5e0).
/// </summary>
public static class SideTrainer
{
    /// <summary>
    /// The trainer whose name heads <paramref name="side"/>: every fighter carries that name as owner and one trainer
    /// id. Null for the group of a boss fight (each fighter links its own owner), the boss, an NPC and a deleted trainer
    /// (no id — never tell them apart by the missing id alone).
    /// </summary>
    public static TrainerRef? Of(BattleSide side) =>
        side.Fighters.Count > 0 && side.Fighters[0].TrainerId is { Length: > 0 } id &&
        side.Fighters.All(f => f.TrainerId == id && f.Owner == side.Name)
            ? new TrainerRef(id, side.Name, side.Fighters[0].Color)
            : null;
}
