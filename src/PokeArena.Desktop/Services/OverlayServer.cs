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
    GameDatabase database,
    SiteGenerator generator,
    TimeProvider time,
    ILogger<OverlayServer> log) : IHostedService, IAsyncDisposable
{
    private WebApplication? _app;

    /// <summary>False if the overlay is switched off or its port was taken.</summary>
    public bool IsRunning => _app is not null;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return;
        }

        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSerilog();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, options.Port));
        var app = builder.Build();
        app.MapGet("/overlay", () => Results.Content(Page.Value, "text/html; charset=utf-8"));
        app.MapGet("/overlay/state", async (CancellationToken ct) =>
        {
            await using var db = database.Open();
            var state = await generator.OverlayAsync(db, time.GetUtcNow(), ct);
            return Results.Text(JsonSerializer.Serialize(state, SiteJson.Default.OverlayState), "application/json; charset=utf-8");
        });

        try
        {
            await app.StartAsync(cancellationToken);
            _app = app;
            log.LogInformation("OBS overlay at {Url}", options.Url);
        }
        catch (IOException ex)
        {
            log.LogWarning(ex, "OBS overlay disabled: port {Port} is in use", options.Port);
            await app.DisposeAsync();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is { } app)
        {
            await app.StopAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is { } app)
        {
            _app = null;
            await app.DisposeAsync();
        }
    }

    private static readonly Lazy<string> Page = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PokeArena.Desktop.Overlay.overlay.html")
            ?? throw new InvalidOperationException("Overlay page missing from the assembly");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });
}
