using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Core;
using PokeArena.Core.Chat;
using PokeArena.Core.Data;
using PokeArena.Core.Game;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Publishing;
using PokeArena.Core.Twitch;

namespace PokeArena.UI.Tests;

/// <summary>The trainer list: registrations, search, delete and the dry run.</summary>
public class TrainerListTests : DesktopTestContext
{
    [Fact]
    public void Empty_list_explains_how_viewers_join()
    {
        var cut = Render<TrainerListPanel>();

        cut.WaitForAssertion(() => Assert.Contains("Noch niemand registriert", cut.Markup));
    }

    [Fact]
    public async Task New_registrations_appear_with_starter_and_count()
    {
        var cut = Render<TrainerListPanel>();

        await _trainers.RegisterAsync("1001", "ash", "Ash", _species.Get(25), _ct);

        cut.WaitForAssertion(() =>
        {
            var cells = cut.FindAll("tbody tr td").Select(td => td.TextContent).ToList();
            Assert.Equal("Ash", cells[0]);
            Assert.Equal("Pikachu", cells[1]);
            Assert.Equal("1", cells[2]);
        });
        Assert.Equal("1 Trainer", cut.Find(".count").TextContent);
    }

    [Fact]
    public async Task Badges_and_tokens_are_listed()
    {
        await _trainers.RegisterAsync("1001", "ash", "Ash", _species.Get(25), _ct);
        await using (var db = _database.Open())
        {
            (await db.Trainers.SingleAsync(_ct)).Tokens = 7;
            db.NpcBadges.AddRange(
                new NpcBadge { TrainerId = "1001", NpcId = "eich", BattleId = 1 },
                new NpcBadge { TrainerId = "1001", NpcId = "rocko", BattleId = 2 });
            await db.SaveChangesAsync(_ct);
        }

        var cut = Render<TrainerListPanel>();

        cut.WaitForAssertion(() =>
        {
            var cells = cut.FindAll("tbody tr td").Select(td => td.TextContent).ToList();
            Assert.Equal(["1", "2", "7"], cells[2..5]);
        });
    }

    [Fact]
    public async Task Search_filters_by_name()
    {
        await _trainers.RegisterAsync("1", "misty", "Misty", _species.Get(7), _ct);
        await _trainers.RegisterAsync("2", "rocko", "Rocko", _species.Get(4), _ct);
        var cut = Render<TrainerListPanel>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tbody tr").Count));

        cut.Find("input.search").Input("mis");

        Assert.Equal("Misty", cut.Find("tbody tr td").TextContent);
        cut.Find("input.search").Input("Brock");
        Assert.Contains("Niemand passt zu", cut.Markup);
    }

    [Fact]
    public async Task Delete_asks_first_then_deletes_on_the_command_queue()
    {
        await _trainers.RegisterAsync("1", "misty", "Misty", _species.Get(7), _ct);
        _time.Advance(TimeSpan.FromMinutes(1));
        await _trainers.RegisterAsync("2", "rocko", "Rocko", _species.Get(4), _ct);
        var cut = Render<TrainerListPanel>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tbody tr").Count));

        cut.FindAll("button.delete")[1].Click();
        Assert.Contains("Misty wirklich löschen?", cut.Find(".confirm-delete").TextContent);
        cut.Find(".confirm-delete .btn-quiet").Click();
        Assert.Empty(cut.FindAll(".confirm-delete"));

        cut.FindAll("button.delete")[1].Click();
        cut.Find(".confirm-delete .btn-danger").Click();
        var work = Assert.IsType<QueuedWork>(await _inbox.Reader.ReadAsync(_ct).AsTask().WaitAsync(TimeSpan.FromSeconds(5), _ct));
        await work.RunAsync(_ct);

        cut.WaitForAssertion(() => Assert.Equal("Rocko", Assert.Single(cut.FindAll("tbody tr td.strong")).TextContent));
    }

    [Fact]
    public async Task Dry_run_shows_only_test_trainers_and_says_so()
    {
        await _trainers.RegisterAsync("1", "misty", "Misty", _species.Get(7), _ct);
        var cut = Render<TrainerListPanel>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("tbody tr")));

        _dryRun.Enabled = true;

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("nur Testtrainer", cut.Markup);
            Assert.Contains("Noch niemand registriert", cut.Markup);
        });
    }
}
