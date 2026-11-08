using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PokeArena.Contracts;
using PokeArena.Core.Data;
using PokeArena.Core.Game;
using PokeArena.Core.Publishing;
using Serilog;

namespace PokeArena.Desktop.Services;

/// <summary>
/// Serves the OBS overlay on the loopback interface (spec §9): <c>/overlay</c> is the page, <c>/overlay/state</c>
/// the latest fight and the top of the leaderboard. Reads the current database, so the dry run can be tried in OBS.
/// A busy port only disables the overlay, never the bot.
/// </summary>
public sealed class OverlayServer(
    OverlayOptions options,
    OverlayStatus status,
    GameDatabase database,
    SiteGenerator generator,
    TrainerService trainers,
    BattleService battles,
    TimeProvider time,
    ILogger<OverlayServer> log) : IHostedService, IAsyncDisposable
{
    /// <summary>A computed state is reused this long at most, even if no change event came.</summary>
    internal static readonly TimeSpan MaxStateAge = TimeSpan.FromSeconds(30);

    private WebApplication? _app;
    private Cached? _cached;
    private int _generation;

    /// <summary>False if the overlay is switched off or its port was taken.</summary>
    public bool IsRunning => _app is not null;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return;
        }

        // OBS polls every 2 s for the whole stream: compute the state only after a change (review 1.0.3, A32).
        trainers.Changed += Invalidate;
        battles.Changed += Invalidate;

        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSerilog();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, options.Port));
        var app = builder.Build();
        app.MapGet("/overlay", () => Results.Content(Page.Value, "text/html; charset=utf-8"));
        app.MapGet("/overlay/state", async (CancellationToken ct) =>
            Results.Text(await StateJsonAsync(ct), "application/json; charset=utf-8"));

        try
        {
            await app.StartAsync(cancellationToken);
            _app = app;
            log.LogInformation("OBS overlay at {Url}", options.Url);
        }
        catch (IOException ex)
        {
            log.LogWarning(ex, "OBS overlay disabled: port {Port} is in use", options.Port);
            status.ReportPortInUse();
            await app.DisposeAsync();
        }
    }

    private async Task<string> StateJsonAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var generation = Volatile.Read(ref _generation);
        if (Volatile.Read(ref _cached) is { } cached && cached.Generation == generation && now - cached.At < MaxStateAge)
        {
            return cached.Json;
        }

        await using var db = database.Open();
        var json = JsonSerializer.Serialize(await generator.OverlayAsync(db, now, ct), SiteJson.Default.OverlayState);
        Volatile.Write(ref _cached, new Cached(json, now, generation));
        return json;
    }

    private void Invalidate() => Interlocked.Increment(ref _generation);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is { } app)
        {
            await app.StopAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        trainers.Changed -= Invalidate;
        battles.Changed -= Invalidate;
        if (_app is { } app)
        {
            _app = null;
            await app.DisposeAsync();
        }
    }

    /// <param name="Generation">The change count the state was computed at; a newer change makes it stale.</param>
    private sealed record Cached(string Json, DateTimeOffset At, int Generation);

    private static readonly Lazy<string> Page = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PokeArena.Desktop.Overlay.overlay.html")
            ?? throw new InvalidOperationException("Overlay page missing from the assembly");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });
}
