using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Core.Chat;
using PokeArena.Core.Twitch;
using PokeArena.Core.Twitch.Auth;

namespace PokeArena.UI.Tests;

public class ComponentTests : BunitContext
{
    private readonly FakeTwitchConnection _twitch = new();
    private readonly ChatLog _chatLog = new(new FakeTimeProvider());
    private readonly ChatInbox _inbox = new();
    private readonly DryRunMode _dryRun = new();

    public ComponentTests()
    {
        Services.AddSingleton<ITwitchConnection>(_twitch);
        Services.AddSingleton(_chatLog);
        Services.AddSingleton(_inbox);
        Services.AddSingleton(_dryRun);
    }

    [Fact]
    public void Signed_out_offers_the_login_and_starts_it()
    {
        _twitch.State = new TwitchConnectionState(TwitchStatus.SignedOut);
        var cut = Render<ConnectionPanel>();

        cut.Find("button.btn-primary").Click();

        Assert.Contains("Bot ist nicht angemeldet", cut.Markup);
        Assert.Equal(1, _twitch.LoginStarts);
    }

    [Fact]
    public void Waiting_for_login_shows_the_code_and_the_activation_link()
    {
        var cut = Render<ConnectionPanel>();

        _twitch.Set(new TwitchConnectionState(
            TwitchStatus.WaitingForLogin,
            Login: new DeviceAuthorization("dev", "ABCDEFGH", new Uri("https://www.twitch.tv/activate"), DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(5))));

        cut.WaitForAssertion(() => Assert.Equal("ABCDEFGH", cut.Find("[data-testid=user-code]").TextContent));
        Assert.Equal("https://www.twitch.tv/activate", cut.Find("ol.steps a").GetAttribute("href"));
        Assert.Contains("Bot-Account", cut.Markup);
    }

    [Fact]
    public void Connected_shows_bot_and_channel()
    {
        _twitch.State = new TwitchConnectionState(TwitchStatus.Connected, BotLogin: "pokearenabot");

        var cut = Render<ConnectionPanel>();

        Assert.Contains("pokearenabot", cut.Find("dl.facts").TextContent);
        Assert.Contains("shiyofurball", cut.Find("dl.facts").TextContent);
    }

    [Theory]
    [InlineData(TwitchProblem.ChannelNotFound, "Den Twitch-Kanal „shiyofurball“ gibt es nicht")]
    [InlineData(TwitchProblem.MissingPermission, "Moderator")]
    public void Failed_explains_the_problem_and_offers_a_retry(TwitchProblem problem, string expected)
    {
        _twitch.State = new TwitchConnectionState(TwitchStatus.Failed, problem);
        var cut = Render<ConnectionPanel>();

        cut.Find("button.btn-primary").Click();

        Assert.Contains(expected, cut.Find(".problem").TextContent);
        Assert.Equal(1, _twitch.Connects);
    }

    [Fact]
    public void Dry_run_form_appears_when_enabled_and_posts_a_simulated_message()
    {
        var cut = Render<DryRunPanel>();
        Assert.Empty(cut.FindAll("form"));

        cut.Find("input[type=checkbox]").Change(true);
        cut.Find("input[aria-label=Nachricht]").Input("!pokearena");
        cut.Find("form").Submit();

        Assert.True(_dryRun.Enabled);
        Assert.True(_inbox.Reader.TryRead(out var message));
        Assert.Equal("!pokearena", message.Text);
        Assert.Equal("Testzuschauer", message.DisplayName);
        Assert.True(message.IsSimulated);
        Assert.StartsWith("probelauf:", message.UserId);
        Assert.Equal("", cut.Find("input[aria-label=Nachricht]").GetAttribute("value"));
    }

    [Fact]
    public void Chat_log_lists_new_entries_first_and_marks_the_dry_run()
    {
        var cut = Render<ChatLogPanel>();
        Assert.Contains("Noch keine Nachrichten", cut.Markup);

        _chatLog.Add(ChatLogKind.Incoming, "Ash", "!pokearena", dryRun: true);
        _chatLog.Add(ChatLogKind.Outgoing, "pokearenabot", "PokeArena-Bot ist online (Version 0.1.0).", dryRun: true);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("li.log-entry").Count));
        var entries = cut.FindAll("li.log-entry");
        Assert.Contains("PokeArena-Bot ist online", entries[0].TextContent);
        Assert.Contains("log-outgoing", entries[0].ClassName);
        Assert.Equal(2, cut.FindAll(".tag").Count);
    }

    private sealed class FakeTwitchConnection : ITwitchConnection
    {
        public TwitchConnectionState State { get; set; } = new(TwitchStatus.Disconnected);

        public string Channel => "shiyofurball";

        public int LoginStarts { get; private set; }

        public int Connects { get; private set; }

        public event Action? StateChanged;

        public void Set(TwitchConnectionState state)
        {
            State = state;
            StateChanged?.Invoke();
        }

        public Task BeginLoginAsync()
        {
            LoginStarts++;
            return Task.CompletedTask;
        }

        public void CancelLogin()
        {
        }

        public void Connect() => Connects++;

        public Task DisconnectAsync() => Task.CompletedTask;

        public Task SignOutAsync() => Task.CompletedTask;
    }
}
