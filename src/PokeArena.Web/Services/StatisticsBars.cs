using PokeArena.Contracts;

namespace PokeArena.Web.Services;

/// <summary>
/// The bars of the statistics page (feat/statistics-page), built like the „Schaden“ box: trainers in their chat colour
/// with a link to their page, bosses in the colour of their first type, a pair of rivals and the community in the HP
/// bar's green. Each bar's length is relative to the longest of its list.
/// </summary>
public static class StatisticsBars
{
    /// <summary>Distinct species the community owns, out of all.</summary>
    public static Bar Community(CommunityTotals c) =>
        Bar.Make(null, "Arten", null, $"{Bar.Number(c.Species)} / {Bar.Number(c.SpeciesTotal)}", c.Species, c.SpeciesTotal, Bar.Mixed);

    public static IReadOnlyList<Bar> BossParticipations(IReadOnlyList<TrainerCount> rows) =>
        [.. rows.Select(r => Trainer(r.Rank, r.Trainer, null, Bar.Number(r.Count), r.Count, Longest(rows, x => x.Count)))];

    /// <summary>„12 Kämpfe · 8 Siege“; the bar measures the fights.</summary>
    public static IReadOnlyList<Bar> PvpFights(IReadOnlyList<PvpRecord> rows) =>
        [.. rows.Select(r => Trainer(
            r.Rank, r.Trainer, null, $"{Fights(r.Fights)} · {Count(r.Wins, "Sieg", "Siege")}", r.Fights, Longest(rows, x => x.Fights)))];

    /// <summary>A streak that is still going says so after the name.</summary>
    public static IReadOnlyList<Bar> BadLuck(IReadOnlyList<BadLuckStreak> rows) =>
        [.. rows.Select(r => Trainer(
            r.Rank, r.Trainer, r.Running ? "(läuft noch)" : null, Bar.Number(r.Length), r.Length, Longest(rows, x => x.Length)))];

    /// <summary>„Anna – Ben“ with „11 Kämpfe · 7 : 4“, the one with more wins first.</summary>
    public static IReadOnlyList<Bar> Rivals(IReadOnlyList<Rivalry> rows) =>
        [.. rows.Select(r => Bar.Make(
            Rank(r.Rank), $"{r.First.DisplayName} – {r.Second.DisplayName}", null,
            $"{Fights(r.Fights)} · {Bar.Number(r.FirstWins)} : {Bar.Number(r.SecondWins)}",
            r.Fights, Longest(rows, x => x.Fights), Bar.Mixed))];

    public static IReadOnlyList<Bar> Bosses(IReadOnlyList<BossTally> rows) =>
        [.. rows.Select(r => Bar.Make(
            Rank(r.Rank), r.Species.Name, null, Bar.Number(r.Count), r.Count, Longest(rows, x => x.Count),
            r.Species.Types.Count > 0 ? Tint.TypeColors.GetValueOrDefault(Format.TypeClass(r.Species.Types[0])) : null))];

    /// <summary>The trainer page of <paramref name="trainer"/>, relative like every link of the site.</summary>
    public static string TrainerLink(TrainerRef trainer) => $"trainer/{Uri.EscapeDataString(trainer.TrainerId)}";

    private static Bar Trainer(int rank, TrainerRef trainer, string? detail, string value, int amount, long longest) =>
        Bar.Make(Rank(rank), trainer.DisplayName, detail, value, amount, longest, trainer.Color, TrainerLink(trainer));

    private static string Rank(int rank) => $"{Bar.Number(rank)}.";

    private static long Longest<T>(IReadOnlyList<T> rows, Func<T, int> value) => rows.Count == 0 ? 0 : rows.Max(x => (long)value(x));

    private static string Fights(int fights) => Count(fights, "Kampf", "Kämpfe");

    private static string Count(int count, string one, string many) => $"{Bar.Number(count)} {(count == 1 ? one : many)}";
}
