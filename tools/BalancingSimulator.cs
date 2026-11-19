// Monte Carlo balancing simulator on the real battle engine (ADR-0006, WP-03).
// Usage (from the repository root): dotnet run tools/BalancingSimulator.cs [-- options]
//
// Options (defaults: the code defaults, BattleSettings/BossSettings = spec §13):
//   --scaling 20      Scaling
//   --damping 50      Damping (k)
//   --boss-hp 1.3     BossHpPerParticipant
//   --hp-common, --hp-rare, --hp-legendary
//                     BossHpPerParticipant for that boss tier only (default: --boss-hp; idea of 2026-11-19, not adopted)
//   --divisor 3       BossTargetDivisor
//   --duels 10        repetitions per ordered 1-vs-1 pairing (151 × 150 pairings)
//   --bosses 5000     boss encounters per participant count (part 2) and per catches × participant count (part 3)
//   --campaigns 500   simulated stream histories (part 4)
//   --encounters 500  boss fights per campaign (part 4)
//   --part 0          run only this part (1–4); 0 runs all
//
// Part 1 reproduces the 1-vs-1 table of ADR-0006, part 2 the boss table, part 3 compares the fielded-Pokemon pick
// by specimen and by species (feat/boss-pick-per-species), part 4 plays whole campaigns in which trainers own only
// what they actually caught (WP-09). Seeds are fixed, so a rerun with the same options prints the same numbers —
// also with --part; parts 3 and 4 have their own seed bases, so --duels does not change them.

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
var campaigns = (int)options["campaigns"];
var encounters = (int)options["encounters"];
var onlyPart = (int)options["part"];
bool Runs(int part) => onlyPart == 0 || onlyPart == part;
double[] tierHp = [0, options["hp-common"], options["hp-rare"], options["hp-legendary"]];

// The boss as BossDraw.Fighter builds it, with the HP per participant of its tier (WP-09 experiment).
Fighter BossFighter(Species s, int n) => tierHp[s.Rarity] < 0
    ? BossDraw.Fighter(s, battle, boss, n)
    : Fighter.Create(s, battle, trainerId: null, owner: "Boss", hpMultiplier: tierHp[s.Rarity] * n);

var species = SpeciesCatalog.Load("data/pokemon.json");
var types = TypeChart.Load("data/typen.json", species);
var damage = new DamageCalculator(battle, types);
var bossBattle = new BossBattle(damage);

Console.WriteLine($"Settings: Scaling {battle.Scaling}, k {battle.Damping}, Spread {battle.SpreadMin}–{battle.SpreadMax}, " +
    $"Crit {battle.CritChance:P2} × {battle.CritFactor}, BossHpPerParticipant {boss.HpPerParticipant}, BossTargetDivisor {boss.TargetDivisor}" +
    (tierHp.Skip(1).Any(h => h >= 0) ? $", by tier common/rare/legendary {string.Join(" / ", tierHp.Skip(1).Select(h => h < 0 ? boss.HpPerParticipant : h))}" : ""));

// Part 1: every ordered pairing of two different species, 1-vs-1, challenger strikes first.
if (Runs(1))
{
    Console.WriteLine();
    var duel = new TeamBattle(damage);
    var wins = new int[species.All.Count + 1];
    var fights = new int[species.All.Count + 1];
    long total = 0, oneHit = 0, challengerWins = 0, attacksSum = 0;
    var longest = 0;
    var duelSeed = 0;
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
                    new BattleRandom(duelSeed++));
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
}

// Part 2: boss encounters. Participants field species drawn like bosses (a collection built from boss catches).
// Its seeds continue where part 1's end, as before --part existed, so its numbers stay comparable.
if (Runs(2))
{
    Console.WriteLine();
    var seed = species.All.Count * (species.All.Count - 1) * duelRepetitions;
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
            var result = bossBattle.Fight(BossFighter(bossSpecies, n), party, boss.TargetDivisor, random);
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
}

// Part 3: which owned Pokemon a participant fields (feat/boss-pick-per-species). Each participant owns a starter
// plus `catches` boss catches drawn by the tier odds of a 5-trainer lobby; "specimen" picks a random specimen (up to
// 1.3.0), "species" a random distinct species (BossDraw.FieldedSpecies). Both modes use the same seed per encounter
// — same boss, same collections — so the difference is the pick alone. Its seeds start at a fixed base, so its
// numbers depend on --bosses (and the balancing options) but not on --duels. Tiers fielded: common / rare /
// legendary share — legendaries duplicate most (5 species), so the species pick fields them less often.
if (Runs(3))
{
    Console.WriteLine();
    Console.WriteLine("Fielded Pokemon: random specimen (up to 1.3.0) vs. random distinct species (participants own starter + catches):");
    Console.WriteLine("  catches (distinct) |   N | group wins spec → species | catch/participant spec → species | fielded common/rare/legendary spec → species");
    var part3Seed = 3_000_000;
    foreach (var catches in new[] { 5, 15, 40, 100 })
    {
        foreach (var n in new[] { 2, 3, 4, 5, 8, 12 })
        {
            var groupWins = new int[2];
            var caught = new int[2];
            var tiers = new int[2, 4];
            long distinctSum = 0;
            for (var r = 0; r < bossRepetitions; r++)
            {
                var encounterSeed = part3Seed++;
                for (var mode = 0; mode < 2; mode++)
                {
                    var random = new BattleRandom(encounterSeed);
                    var bossSpecies = BossDraw.Species(species, boss, n, random);
                    var collections = Enumerable.Range(0, n)
                        .Select(_ => (List<int>)[random.Pick(species.Starters).Id, .. Enumerable.Range(0, catches).Select(_ => BossDraw.Species(species, boss, 5, random).Id)])
                        .ToList();
                    var fielded = collections
                        .Select(c => mode == 0 ? random.Pick(c) : BossDraw.FieldedSpecies(c, random))
                        .ToList();
                    var party = fielded.Select((id, i) => Fighter.Create(species.Get(id), battle, $"t{i}", $"T{i}")).ToList();
                    var result = bossBattle.Fight(BossFighter(bossSpecies, n), party, boss.TargetDivisor, random);
                    groupWins[mode] += result.BossDefeated ? 1 : 0;
                    caught[mode] += result.BossDefeated ? result.Survived.Count(s => s) : 0;
                    foreach (var id in fielded)
                    {
                        tiers[mode, species.Get(id).Rarity]++;
                    }

                    distinctSum += mode == 0 ? collections.Sum(c => c.Distinct().Count()) : 0;
                }
            }

            double Share(int count, int per) => (double)count / (bossRepetitions * per);
            string Tiers(int mode) => $"{Share(tiers[mode, 1], n) * 100:F0}/{Share(tiers[mode, 2], n) * 100:F0}/{Share(tiers[mode, 3], n) * 100:F1} %";
            Console.WriteLine(
                $"  {catches,7} ({(double)distinctSum / (bossRepetitions * n),5:F1})    | {n,3} | {Share(groupWins[0], 1),10:P0} → {Share(groupWins[1], 1),-12:P0} | " +
                $"{Share(caught[0], n),17:P0} → {Share(caught[1], n),-12:P0} | {Tiers(0),19} → {Tiers(1)}");
        }
    }
}

// Part 4: campaigns (WP-09). Parts 2 and 3 let participants field species drawn like bosses, but a trainer only owns
// the bosses their group beat — mostly the weak ones — so real collections stay weaker than the bosses they face: the
// 182 stream boss fights up to 2026-11-18 fielded an average stat sum (HP + Atk + Def + Spez) of 202 against bosses
// of 265, won 50.5 % and caught 37.7 % per participant. A campaign replays such a history: a fixed pool of trainers,
// each starting with a random starter and adding the boss whenever they survive a won fight. Lobby sizes follow
// those stream fights (N weights below, counts per lobby size only). Who joins is a model of a small channel, not
// the stream's per-viewer counts (no player data in the repository, ADR-0003): every trainer joins 0.85 × as often
// as the one before (a round decay, not fitted to anyone), scaled to the mean lobby size. Drawing a
// lobby one by one by weight evens those rates out, so the weights are calibrated first (fixed seeds) until the
// simulated rates match. PvP transfers and NPC rewards are left out. Seeds: one per campaign from a fixed base.
if (Runs(4))
{
    Console.WriteLine();
    double[] lobbyWeights = [0, 0, 14, 38, 53, 34, 23, 15, 5]; // index = N, stream fights 1–203
    double[] joinModel = [.. Enumerable.Range(0, 12).Select(i => Math.Pow(0.85, i))];
    var meanN = lobbyWeights.Select((w, n) => w * n).Sum() / lobbyWeights.Sum();
    var joinTarget = joinModel.Select(r => r * meanN / joinModel.Sum()).ToArray();
    var (trainerWeights, joinSimulated) = CalibrateJoins(lobbyWeights, joinTarget);
    const int streamFights = 182;
    var phaseEnds = new[] { 50, 100, streamFights, 300, 500, 1000, 2000 }.Where(e => e < encounters).Append(encounters).ToArray();
    var phases = phaseEnds.Length;
    var pFights = new long[phases];
    var pWins = new long[phases];
    var pSlots = new long[phases];
    var pCaught = new long[phases];
    var pFieldedSum = new long[phases];
    var pBossSum = new long[phases];
    var pOwned = new double[phases];
    var pTiers = new long[phases, 4];
    var maxN = lobbyWeights.Length - 1;
    var nFights = new long[maxN + 1];
    var nWins = new long[maxN + 1];
    var nCaught = new long[maxN + 1];
    var tierFights = new long[4];
    var tierWins = new long[4];
    int[] strengthBounds = [250, 330, 400]; // boss stat sum buckets as in the stream evaluation
    var strengthFights = new long[strengthBounds.Length + 1];
    var strengthWins = new long[strengthBounds.Length + 1];

    for (var c = 0; c < campaigns; c++)
    {
        var random = new BattleRandom(4_000_000 + c);
        var owned = trainerWeights.Select(_ => new HashSet<int> { random.Pick(species.Starters).Id }).ToList();
        var phase = 0;
        for (var e = 0; e < encounters; e++)
        {
            var lobby = DrawLobby(lobbyWeights, trainerWeights, random);
            var n = lobby.Count;
            var bossSpecies = BossDraw.Species(species, boss, n, random);
            var fielded = lobby.Select(t => species.Get(BossDraw.FieldedSpecies(owned[t], random))).ToList();
            var party = fielded.Select((s, i) => Fighter.Create(s, battle, $"t{i}", $"T{i}")).ToList();
            var result = bossBattle.Fight(BossFighter(bossSpecies, n), party, boss.TargetDivisor, random);
            var caught = 0;
            for (var i = 0; i < n; i++)
            {
                if (result.BossDefeated && result.Survived[i])
                {
                    owned[lobby[i]].Add(bossSpecies.Id);
                    caught++;
                }
            }

            pFights[phase]++;
            pWins[phase] += result.BossDefeated ? 1 : 0;
            pSlots[phase] += n;
            pCaught[phase] += caught;
            pFieldedSum[phase] += fielded.Sum(StatSum);
            pBossSum[phase] += StatSum(bossSpecies);
            foreach (var s in fielded)
            {
                pTiers[phase, s.Rarity]++;
            }

            if (e < streamFights)
            {
                nFights[n]++;
                nWins[n] += result.BossDefeated ? 1 : 0;
                nCaught[n] += caught;
                tierFights[bossSpecies.Rarity]++;
                tierWins[bossSpecies.Rarity] += result.BossDefeated ? 1 : 0;
                var bucket = strengthBounds.Count(b => StatSum(bossSpecies) >= b);
                strengthFights[bucket]++;
                strengthWins[bucket] += result.BossDefeated ? 1 : 0;
            }

            if (e + 1 == phaseEnds[phase])
            {
                pOwned[phase] += owned.Average(o => o.Count);
                phase++;
            }
        }
    }

    string Share(long count, long total) => total == 0 ? "-" : $"{(double)count / total:P0}";
    Console.WriteLine($"Campaigns ({campaigns} × {encounters} boss fights, {trainerWeights.Length} trainers own a starter and what they caught; lobby sizes as on stream):");
    Console.WriteLine($"  joins per fight, model → simulated: " +
        string.Join(" ", joinTarget.Select((t, i) => $"{t * 100:F0}→{joinSimulated[i] * 100:F0}")) + " %");
    var firstFights = nFights.Sum();
    Console.WriteLine($"  {$"first {firstFights / Math.Max(campaigns, 1)} fights by N",21} | share | group wins | catch/participant");
    for (var n = 2; n <= maxN; n++)
    {
        Console.WriteLine($"  {n,21} | {Share(nFights[n], firstFights),5} | {Share(nWins[n], nFights[n]),10} | {Share(nCaught[n], nFights[n] * n),17}");
    }

    Console.WriteLine($"  {"all",21} |       | {(double)nWins.Sum() / firstFights,10:P1} | " +
        $"{(double)nCaught.Sum() / Enumerable.Range(0, maxN + 1).Sum(n => nFights[n] * n),17:P1}");
    Console.WriteLine($"  group wins by boss tier common / rare / legendary: {Share(tierWins[1], tierFights[1])} / " +
        $"{Share(tierWins[2], tierFights[2])} / {Share(tierWins[3], tierFights[3])}");
    Console.WriteLine($"  group wins by boss stat sum < 250 / 250–329 / 330–399 / ≥ 400: " +
        string.Join(" / ", strengthWins.Select((w, i) => Share(w, strengthFights[i]))));
    Console.WriteLine();
    Console.WriteLine("        fights | group wins | catch/participant | stat sum fielded / boss | species owned (end) | fielded common/rare/legendary");
    for (var p = 0; p < phases; p++)
    {
        var from = p == 0 ? 1 : phaseEnds[p - 1] + 1;
        Console.WriteLine(
            $"  {$"{from}–{phaseEnds[p]}",12} | {(double)pWins[p] / pFights[p],10:P0} | {(double)pCaught[p] / pSlots[p],17:P0} | " +
            $"{(double)pFieldedSum[p] / pSlots[p],11:F0} / {(double)pBossSum[p] / pFights[p],-9:F0} | {pOwned[p] / campaigns,19:F1} | " +
            $"{100.0 * pTiers[p, 1] / pSlots[p]:F0}/{100.0 * pTiers[p, 2] / pSlots[p]:F0}/{100.0 * pTiers[p, 3] / pSlots[p]:F1} %");
    }
}

static int StatSum(Species s) => s.Hp + s.Atk + s.Def + s.Spez;

// An index drawn proportionally to its weight; zero weights are never drawn.
static int Weighted(IReadOnlyList<double> weights, BattleRandom random)
{
    var roll = random.NextDouble() * weights.Sum();
    var last = 0;
    for (var i = 0; i < weights.Count; i++)
    {
        if (weights[i] == 0)
        {
            continue;
        }

        last = i;
        roll -= weights[i];
        if (roll < 0)
        {
            return i;
        }
    }

    return last;
}

// A lobby: its size by lobbyWeights, then that many distinct trainers, one after another by joinWeights.
static List<int> DrawLobby(IReadOnlyList<double> lobbyWeights, IReadOnlyList<double> joinWeights, BattleRandom random)
{
    var n = Weighted(lobbyWeights, random);
    var remaining = joinWeights.ToArray();
    var lobby = new List<int>();
    for (var k = 0; k < n && k < remaining.Length; k++)
    {
        var t = Weighted(remaining, random);
        lobby.Add(t);
        remaining[t] = 0;
    }

    return lobby;
}

// Join weights whose simulated share of fights joined matches target (per trainer): start at the target, then
// w ← w × target / simulated a fixed number of times on fixed seeds. Returns the weights and the rates they reach.
static (double[] Weights, double[] Simulated) CalibrateJoins(IReadOnlyList<double> lobbyWeights, IReadOnlyList<double> target)
{
    const int lobbies = 20_000;
    var weights = target.ToArray();
    var simulated = new double[weights.Length];
    for (var iteration = 0; iteration <= 30; iteration++)
    {
        if (iteration > 0)
        {
            for (var i = 0; i < weights.Length; i++)
            {
                weights[i] *= target[i] / Math.Max(simulated[i], 1e-9);
            }
        }

        var joins = new int[weights.Length];
        var random = new BattleRandom(4_900_000);
        for (var l = 0; l < lobbies; l++)
        {
            foreach (var t in DrawLobby(lobbyWeights, weights, random))
            {
                joins[t]++;
            }
        }

        simulated = [.. joins.Select(j => (double)j / lobbies)];
    }

    return (weights, simulated);
}

static Dictionary<string, double> ParseOptions(string[] args)
{
    var options = new Dictionary<string, double>
    {
        // The code defaults (spec §13), so the simulator follows a changed default (review 1.0.3, B6).
        ["scaling"] = BattleSettings.Default.Scaling,
        ["damping"] = BattleSettings.Default.Damping,
        ["boss-hp"] = BossSettings.Default.HpPerParticipant,
        ["divisor"] = BossSettings.Default.TargetDivisor,
        ["duels"] = 10,
        ["bosses"] = 5000,
        ["campaigns"] = 500,
        ["encounters"] = 500,
        ["part"] = 0,
        ["hp-common"] = -1,
        ["hp-rare"] = -1,
        ["hp-legendary"] = -1,
    };
    if (args.Length % 2 != 0)
    {
        throw new ArgumentException($"Option {args[^1]} has no value");
    }

    for (var i = 0; i + 1 < args.Length; i += 2)
    {
        var key = args[i].TrimStart('-');
        if (!options.ContainsKey(key))
        {
            throw new ArgumentException($"Unknown option {args[i]}");
        }

        options[key] = double.Parse(args[i + 1], CultureInfo.InvariantCulture);
    }

    if (options["part"] is < 0 or > 4 || options["part"] % 1 != 0)
    {
        throw new ArgumentException($"--part must be 0 (all) or 1–4, not {options["part"]}");
    }

    return options;
}
