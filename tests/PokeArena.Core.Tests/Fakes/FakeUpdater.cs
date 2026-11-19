namespace PokeArena.Core.Tests.Fakes;

/// <summary>
/// An update source the test controls. Shared with PokeArena.UI.Tests (linked there), so both suites drive the same
/// fake (review 1.8.1, C15).
/// </summary>
public sealed class FakeUpdater : IAppUpdater
{
    public bool SelfUpdate { get; set; } = true;
    public string? Newer { get; set; }
    public bool FailCheck { get; set; }
    public bool FailDownload { get; set; }
    public bool FailInstall { get; set; }
    public int Checks { get; private set; }
    public int Downloads { get; private set; }
    public int Installs { get; private set; }

    /// <summary>A check that answers only when the test says so.</summary>
    public Task<string?>? Pending { get; set; }

    public bool CanSelfUpdate => SelfUpdate;

    public Task<string?> FindNewerAsync(CancellationToken ct)
    {
        Checks++;
        if (Pending is { } pending)
        {
            return pending;
        }

        return FailCheck ? throw new HttpRequestException("offline") : Task.FromResult(Newer);
    }

    public Task DownloadAsync(CancellationToken ct)
    {
        Downloads++;
        return FailDownload ? throw new HttpRequestException("broken download") : Task.CompletedTask;
    }

    public void RestartIntoUpdate()
    {
        if (FailInstall)
        {
            throw new InvalidOperationException("Update.exe missing");
        }

        Installs++;
    }
}
