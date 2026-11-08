using PokeArena.Contracts;
using PokeArena.Core.Data;

namespace PokeArena.Core.Game.Battles;

/// <summary>Headline and result of a stored fight, and who stood on which side.</summary>
/// <param name="TrainerSide">Name of side 0 of the log: „Trainer“ (boss), the challenger, or the trainer (NPC).</param>
/// <param name="OpponentSide">Name of side 1: the boss, the challenged trainer, or the NPC.</param>
/// <param name="TrainerSideWon">Side 0 won: the boss was defeated, the challenger or the trainer won.</param>
public sealed record BattleDescription(string Headline, string Result, string TrainerSide, string OpponentSide, bool TrainerSideWon);

/// <summary>
/// The German texts of a stored fight, the same on the website and in the desktop app (review 1.0.3, B1). Names come
/// from the log — as they were at the fight, „[gelöscht]“ for a deleted trainer. Chat messages are written when the
/// fight happens and have their own, reviewed wording (<c>docs/chat-texts.md</c>); they share the small helpers here.
/// </summary>
public sealed class BattleTexts(SpeciesCatalog species, NpcCatalog npcs)
{
    /// <summary>„ (legendär)“ or „ (selten)“ after a boss name; nothing for a common boss.</summary>
    public static string TierSuffix(Species boss) => boss.Rarity switch
    {
        3 => " (legendär)",
        2 => " (selten)",
        _ => "",
    };

    /// <summary>„1 Token“ or „3 Tokens“.</summary>
    public static string Tokens(int count) => count == 1 ? "1 Token" : $"{count} Tokens";

    public BattleDescription Describe(Battle battle, BattleLog log)
    {
        var trainerSide = log.Fighters.Where(f => f.Side == 0).ToList();
        var opponentSide = log.Fighters.Where(f => f.Side == 1).ToList();
        var loot = battle.LootSpeciesId is { } lootId ? species.Get(lootId).Name : null;
        switch (battle.Kind)
        {
            case BattleKind.Boss:
            {
                var boss = species.Get(battle.BossSpeciesId ?? opponentSide[0].SpeciesId);
                var defeated = battle.Outcome == BattleOutcome.BossDefeated;
                // From the log, not the participant rows: survivors are the catchers (spec §4), and a deleted
                // trainer still counts as „[gelöscht]“.
                var catchers = log.Survivors()
                    .Select(i => log.Fighters[i])
                    .Where(f => f.Side == 0)
                    .Select(f => f.Owner)
                    .ToList();
                var result = defeated
                    ? $"{boss.Name} wurde besiegt! Gefangen von: {string.Join(", ", catchers)}."
                    : $"{boss.Name} ist entkommen.";
                return new BattleDescription($"Boss-Kampf gegen {boss.Name}{TierSuffix(boss)}", result, "Trainer", boss.Name, defeated);
            }

            case BattleKind.Pvp:
            {
                var challenger = trainerSide[0].Owner;
                var opponent = opponentSide[0].Owner;
                var challengerWon = battle.Outcome == BattleOutcome.ChallengerWon;
                var (winner, loser) = challengerWon ? (challenger, opponent) : (opponent, challenger);
                var result = loot is null ? $"{winner} besiegt {loser}." : $"{winner} besiegt {loser} und bekommt {loot} von {loser}!";
                return new BattleDescription($"{challenger} gegen {opponent}", result, challenger, opponent, challengerWon);
            }

            case BattleKind.Npc:
            {
                var trainer = trainerSide[0].Owner;
                var npc = battle.NpcId is { } id ? npcs.Find(id)?.Name ?? id : opponentSide[0].Owner;
                var won = battle.Outcome == BattleOutcome.ChallengerWon;
                var result = !won
                    ? $"{npc} besiegt {trainer}."
                    : battle.Tokens is { } tokens
                        ? $"{trainer} besiegt {npc} zum ersten Mal und bekommt den Orden, {Tokens(tokens)}{(loot is null ? "" : $" und {loot}")}!"
                        : $"{trainer} besiegt {npc}.";
                return new BattleDescription($"{trainer} gegen {npc}", result, trainer, npc, won);
            }

            default:
                throw new InvalidOperationException($"Unknown battle kind {battle.Kind}");
        }
    }
}
