// Monte Carlo balancing simulator on the real battle engine (ADR-0006, WP-03).
// Usage (from the repository root): dotnet run tools/BalancingSimulator.cs [-- options]
//
// Options (defaults = spec §13):
//   --scaling 20      Skalierung
//   --damping 50      Daempfung (k)
//   --boss-hp 1.8     BossHPProSpieler
//   --divisor 3       BossZieleTeiler
//   --duels 10        repetitions per ordered 1-vs-1 pairing (151 × 150 pairings)
//   --bosses 5000     boss encounters per participant count
//
// Part 1 reproduces the 1-vs-1 table of ADR-0006, part 2 the boss table. Seeds are fixed, so a rerun with the
// same options prints the same numbers.

#:project ../src/PokeArena.Core/PokeArena.Core.csproj
// File-based apps default to AOT, which disables the reflection-based JSON of the base data loaders.
#:property PublishAot=false

using System.Globalization;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var options = ParseOptions(args);
var battle = BattleSettings.Default with { Scaling = options["scaling"], Damping = options["damping"] };
var boss = BossSettings.Default with { HpPerParticipant = options["boss-hp"], TargetDivisor = (int)options["divisor"] };
var duelRepetitions = (int)options["duels"];
var bossRepetitions = (int)options["bosses"];

var species = SpeciesCatalog.Load("data/pokemon.json");
var types = TypeChart.Load("data/typen.json", species);
var damage = new DamageCalculator(battle, types);

Console.WriteLine($"Settings: Skalierung {battle.Scaling}, k {battle.Damping}, Streuung {battle.SpreadMin}–{battle.SpreadMax}, " +
    $"Krit {battle.CritChance:P2} × {battle.CritFactor}, BossHPProSpieler {boss.HpPerParticipant}, BossZieleTeiler {boss.TargetDivisor}");
Console.WriteLine();

// Part 1: every ordered pairing of two different species, 1-vs-1, challenger strikes first.
var duel = new TeamBattle(damage);
var wins = new int[species.All.Count + 1];
var fights = new int[species.All.Count + 1];
long total = 0, oneHit = 0, challengerWins = 0, attacksSum = 0;
var longest = 0;
var seed = 0;
foreach (var a in species.All)
{
    foreach (var b in species.All)
    {
        if (a.Id == b.Id)
        {
            continue;
        }

        for (var r = 0; r < duelRepetitions; r++)
        {
            var result = duel.Fight(
                [Fighter.Create(a, battle, null, "A")],
                [Fighter.Create(b, battle, null, "B")],
                new BattleRandom(seed++));
            var winner = result.ChallengerWon ? a.Id : b.Id;
            wins[winner]++;
            fights[a.Id]++;
            fights[b.Id]++;
            total++;
            challengerWins += result.ChallengerWon ? 1 : 0;
            oneHit += result.Attacks == 1 ? 1 : 0;
            attacksSum += result.Attacks;
            longest = Math.Max(longest, result.Attacks);
        }
    }
}

double Rate(int id) => (double)wins[id] / fights[id];
var rates = species.All.Select(s => (s, rate: Rate(s.Id))).OrderBy(x => x.rate).ToList();

Console.WriteLine($"1-vs-1: {total:N0} fights");
Console.WriteLine($"  Chaneira {Rate(113):P0} · Simsala {Rate(65):P0} · Mewtu {Rate(150):P0} · Karpador {Rate(129):P0}");
Console.WriteLine($"  species < 15 % / > 85 %: {rates.Count(x => x.rate < 0.15)} / {rates.Count(x => x.rate > 0.85)}");
Console.WriteLine($"  one-hit KOs: {(double)oneHit / total:P1} · challenger wins (first strike): {(double)challengerWins / total:P1}");
Console.WriteLine($"  hits per fight: avg {(double)attacksSum / total:F1}, longest {longest}");
Console.WriteLine($"  weakest: {string.Join(", ", rates.Take(5).Select(x => $"{x.s.Name} {x.rate:P0}"))}");
Console.WriteLine($"  strongest: {string.Join(", ", rates.TakeLast(5).Reverse().Select(x => $"{x.s.Name} {x.rate:P0}"))}");
Console.WriteLine();

// Part 2: boss encounters. Participants field species drawn like bosses (a collection built from boss catches).
var bossBattle = new BossBattle(damage);
Console.WriteLine("Boss (participants' and boss species drawn by the tier probabilities of spec §4):");
Console.WriteLine("    N | group wins | catch/participant | rounds avg (max) | log lines/participant | round limit");
foreach (var n in new[] { 2, 3, 4, 5, 6, 8, 10, 12, 15, 20 })
{
    int groupWins = 0, catches = 0, roundsMax = 0, limits = 0;
    long roundsSum = 0, linesSum = 0;
    for (var r = 0; r < bossRepetitions; r++)
    {
        var random = new BattleRandom(seed++);
        var bossSpecies = BossDraw.Species(species, boss, n, random);
        var party = Enumerable.Range(0, n)
            .Select(i => Fighter.Create(BossDraw.Species(species, boss, n, random), battle, $"t{i}", $"T{i}"))
            .ToList();
        var result = bossBattle.Fight(BossDraw.Fighter(bossSpecies, battle, boss, n), party, boss.TargetDivisor, random);
        groupWins += result.BossDefeated ? 1 : 0;
        catches += result.BossDefeated ? result.Survived.Count(s => s) : 0;
        roundsSum += result.Rounds;
        roundsMax = Math.Max(roundsMax, result.Rounds);
        limits += result.HitRoundLimit ? 1 : 0;
        linesSum += result.Log.ToLines(species).Count;
    }

    Console.WriteLine(
        $"  {n,3} | {(double)groupWins / bossRepetitions,10:P0} | {(double)catches / (bossRepetitions * n),17:P0} | " +
        $"{(double)roundsSum / bossRepetitions,10:F1} ({roundsMax,3}) | {(double)linesSum / (bossRepetitions * n),21:F1} | {limits}");
}

static Dictionary<string, double> ParseOptions(string[] args)
{
    var options = new Dictionary<string, double>
    {
        ["scaling"] = 20,
        ["damping"] = 50,
        ["boss-hp"] = 1.8,
        ["divisor"] = 3,
        ["duels"] = 10,
        ["bosses"] = 5000,
    };
    for (var i = 0; i + 1 < args.Length; i += 2)
    {
        var key = args[i].TrimStart('-');
        if (!options.ContainsKey(key))
        {
            throw new ArgumentException($"Unknown option {args[i]}");
        }

        options[key] = double.Parse(args[i + 1], CultureInfo.InvariantCulture);
    }

    return options;
}
