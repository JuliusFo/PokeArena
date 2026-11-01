using Microsoft.AspNetCore.Components;

namespace PokeArena.Web.Services;

/// <summary>
/// A page that reloads its data every <see cref="Interval"/> while it is open (spec §9: leaderboard and overlay
/// poll every 15 s). A failed load keeps the last data and shows a hint; the next round tries again.
/// </summary>
public abstract class PollingPage : ComponentBase, IDisposable
{
    /// <summary>WebsitePollSeconds (spec §13).</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    private readonly CancellationTokenSource _stop = new();

    [Inject]
    protected SiteData Data { get; set; } = null!;

    [Inject]
    protected TimeProvider Time { get; set; } = null!;

    /// <summary>The first load is still running.</summary>
    protected bool Loading { get; private set; } = true;

    /// <summary>The last load failed (data host unreachable).</summary>
    protected bool Failed { get; private set; }

    protected abstract Task LoadAsync(CancellationToken ct);

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
        _ = PollAsync();
    }

    private async Task PollAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                await Task.Delay(Interval, Time, _stop.Token);
                await RefreshAsync();
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // Page left.
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            await LoadAsync(_stop.Token);
            Failed = false;
        }
        catch (HttpRequestException)
        {
            Failed = true;
        }
        finally
        {
            Loading = false;
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
        GC.SuppressFinalize(this);
    }
}
