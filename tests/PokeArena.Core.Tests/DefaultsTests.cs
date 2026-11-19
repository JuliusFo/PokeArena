using PokeArena.Core.Chat;
using PokeArena.Core.Commands;
using PokeArena.Core.Game;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Publishing;

namespace PokeArena.Core.Tests;

/// <summary>
/// The defaults of spec §13 and the limits players and Twitch see, as literal values: most tests use these settings
/// through their symbol, so a changed value would go unnoticed while spec, guide page and chat texts still say the old
/// one (review 1.8.1, C3). A change here is a balancing or rule change: update spec §13 and the guide page with it.
/// </summary>
public class DefaultsTests
{
    [Fact]
    public void Battle_defaults_match_spec_13()
    {
        var battle = BattleSettings.Default;

        Assert.Equal(20, battle.Scaling);
        Assert.Equal(50, battle.Damping);
        Assert.Equal((0.85, 1.00), (battle.SpreadMin, battle.SpreadMax));
        Assert.Equal((0.0625, 2.0), (battle.CritChance, battle.CritFactor));
        Assert.Equal(1.0, battle.HpFactor);
        Assert.Equal(200, battle.MaxRounds);
    }

    [Fact]
    public void Boss_defaults_match_spec_4_and_13()
    {
        var boss = BossSettings.Default;

        Assert.Equal(TimeSpan.FromSeconds(120), boss.Countdown);
        Assert.Equal(TimeSpan.FromSeconds(10), boss.LateJoinExtension);
        Assert.Equal(2, boss.MinParticipants);
        Assert.Equal(TimeSpan.FromMinutes(30), boss.Cooldown);
        Assert.Equal(TimeSpan.FromMinutes(2), boss.FailedLobbyCooldown);
        Assert.Equal(1.3, boss.HpPerParticipant);
        Assert.Equal(3, boss.TargetDivisor);
        Assert.Equal(new TierOdds(0.01, 0.01, 0.20), boss.Legendary);
        Assert.Equal(new TierOdds(0.10, 0.02, 0.40), boss.Rare);
    }

    [Fact]
    public void Pvp_and_npc_defaults_match_spec_13()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), PvpSettings.Default.ChallengeTimeout);
        Assert.Equal(6, PvpSettings.Default.TeamSize);
        Assert.Equal(TimeSpan.FromHours(48), NpcSettings.Default.LossCooldown);
        Assert.Equal(TimeSpan.FromMinutes(60), NpcSettings.Default.WinCooldown);
    }

    [Fact]
    public void Chat_limits_match_twitch_and_spec_8()
    {
        // Twitch refuses chat messages over 500 characters: a longer fight result would be dropped.
        Assert.Equal(500, ChatText.MaxLength);
        Assert.Equal(TimeSpan.FromSeconds(30), ChatSender.HintInterval);
        Assert.Equal(TimeSpan.FromSeconds(30), BossEncounter.CooldownReplyInterval);
        Assert.Equal(TimeSpan.FromMinutes(5), CommandProcessor.OfflineHintCooldown);
    }

    [Fact]
    public void Site_limits_match_spec_9()
    {
        Assert.Equal(50, SiteGenerator.BattleIndexSize);
        Assert.Equal(10, SiteGenerator.RecentBattlesPerTrainer);

        // Lists must be fresher than the website's 15 s poll; fights and pictures stay a day (spec §11).
        Assert.Equal("public, max-age=10", SiteGenerator.MutableCache);
        Assert.Equal("public, max-age=86400", SiteGenerator.BattleCache);
    }
}
