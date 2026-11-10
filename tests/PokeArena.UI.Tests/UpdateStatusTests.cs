using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Core;

namespace PokeArena.UI.Tests;

public class UpdateStatusTests : BunitContext
{
    private readonly FakeUpdater _updater = new();
    private readonly FakeGate _gate = new();
    private readonly UpdateCheck _check;

    public UpdateStatusTests()
    {
        _check = new UpdateCheck(_updater, _gate, new FakeTimeProvider(), NullLogger<UpdateCheck>.Instance);
        Services.AddSingleton(_check);
    }

    [Fact]
    public void Checking_by_hand_says_when_the_app_is_current()
    {
        var cut = Render<UpdateStatus>();

        cut.Find("button").Click();

        cut.WaitForAssertion(() => Assert.Equal("Du hast die neueste Version.", cut.Find(".update-answer").TextContent));
        Assert.Empty(cut.FindAll(".tag"));
    }

    [Fact]
    public void A_downloaded_version_offers_the_install_and_hides_the_check()
    {
        _updater.Newer = "1.6.1";
        var cut = Render<UpdateStatus>();

        cut.Find("button").Click();

        cut.WaitForAssertion(() => Assert.Equal("Version 1.6.1 bereit", cut.Find(".tag-update").TextContent));
        var install = Assert.Single(cut.FindAll("button"));
        Assert.Equal("Jetzt aktualisieren", install.TextContent);

        install.Click();

        cut.WaitForAssertion(() => Assert.Contains("startet gleich neu", cut.Find(".tag-update").TextContent));
        Assert.Equal(1, _updater.Installs);
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public async Task The_install_waits_while_a_boss_lobby_is_open()
    {
        _updater.Newer = "1.6.1";
        _gate.Lobby = true;
        await _check.CheckNowAsync(Xunit.TestContext.Current.CancellationToken);

        var cut = Render<UpdateStatus>();

        var install = cut.Find("button.btn-primary");
        Assert.True(install.HasAttribute("disabled"));
        Assert.Equal("Erst wenn die Boss-Lobby vorbei ist.", install.GetAttribute("title"));
    }

    [Fact]
    public async Task A_copy_that_cannot_update_itself_links_the_download()
    {
        _updater.SelfUpdate = false;
        _updater.Newer = "1.6.1";
        await _check.CheckNowAsync(Xunit.TestContext.Current.CancellationToken);

        var cut = Render<UpdateStatus>();

        var link = cut.Find("a.tag-update");
        Assert.Equal("Version 1.6.1 verfügbar", link.TextContent);
        Assert.Equal(UpdateCheck.ReleasesPage, link.GetAttribute("href"));
        Assert.Contains("Setup.exe", link.GetAttribute("title"));
        Assert.Equal("Nach Updates suchen", cut.Find("button").TextContent.Trim());
    }

    [Fact]
    public void A_failed_download_shows_and_can_be_retried()
    {
        _updater.Newer = "1.6.1";
        _updater.FailDownload = true;
        var cut = Render<UpdateStatus>();

        cut.Find("button").Click();

        cut.WaitForAssertion(() => Assert.Equal("Update fehlgeschlagen", cut.Find(".tag-update-failed").TextContent));
        Assert.Empty(cut.FindAll(".update-answer"));

        _updater.FailDownload = false;
        cut.Find("button").Click();

        cut.WaitForAssertion(() => Assert.Equal("Version 1.6.1 bereit", cut.Find(".tag-update").TextContent));
    }

    private sealed class FakeUpdater : IAppUpdater
    {
        public bool SelfUpdate { get; set; } = true;
        public string? Newer { get; set; }
        public bool FailDownload { get; set; }
        public int Installs { get; private set; }

        public bool CanSelfUpdate => SelfUpdate;

        public Task<string?> FindNewerAsync(CancellationToken ct) => Task.FromResult(Newer);

        public Task DownloadAsync(CancellationToken ct) =>
            FailDownload ? throw new HttpRequestException("broken download") : Task.CompletedTask;

        public void RestartIntoUpdate() => Installs++;
    }

    private sealed class FakeGate : IUpdateGate
    {
        public bool Lobby { get; set; }

        public bool LobbyOpen => Lobby;

        public bool MayInstallAutomatically => false;
    }
}
