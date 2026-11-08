using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PokeArena.Core;
using PokeArena.Core.Game;
using PokeArena.Core.Publishing;
using PokeArena.Core.Secrets;
using PokeArena.Core.Twitch;
using PokeArena.Desktop.Services;
using PokeArena.Desktop.Views;
using PokeArena.UI;
using Serilog;
using Serilog.Events;

namespace PokeArena.Desktop;

public partial class App : Application
{
    private IHost? _host;
    private Mutex? _singleInstance;
    private bool _stopping;
    private bool _hostStopped;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterGlobalExceptionHandlers();

        // Two running bots would answer every chat command twice.
        _singleInstance = new Mutex(initiallyOwned: true, @"Local\PokeArena.Desktop", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("PokeArena läuft bereits.", "PokeArena", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Start(e.Args);
    }

    /// <summary>A start that fails says so and ends the process instead of lingering without a window.</summary>
    private async void Start(string[] args)
    {
        try
        {
            await StartAsync(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "PokeArena could not start");
            MessageBox.Show($"PokeArena konnte nicht starten:\n\n{ex.Message}", "PokeArena", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async Task StartAsync(string[] args)
    {
        // Content root = program folder: started from a shortcut, the working directory is anywhere,
        // and appsettings.json would silently not be found.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        // Developer overrides (git-ignored); the command line still wins over both files.
        builder.Configuration.AddJsonFile("appsettings.local.json", optional: true).AddCommandLine(args);

        var options = builder.Configuration.GetSection(PokeArenaOptions.SectionName).Get<PokeArenaOptions>() ?? new();
        var paths = AppPaths.Resolve(options);
        AddStreamerSettings(builder.Configuration, new SettingsFile(paths));
        var twitchOptions = builder.Configuration.GetSection(TwitchOptions.SectionName).Get<TwitchOptions>() ?? new();
        var websiteOptions = builder.Configuration.GetSection(WebsiteOptions.SectionName).Get<WebsiteOptions>() ?? new();
        var overlayOptions = builder.Configuration.GetSection(OverlayOptions.SectionName).Get<OverlayOptions>() ?? new();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            // The OBS overlay polls every 2 s; request logs would flood the file.
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .WriteTo.File(
                Path.Combine(paths.LogsDirectory, "pokearena-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "?";

        builder.Services.AddSerilog();
        builder.Services.Configure<ConsoleLifetimeOptions>(o => o.SuppressStatusMessages = true);
        builder.Services.AddWpfBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
        builder.Services.AddSingleton(paths);
        builder.Services.AddSingleton<SettingsFile>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ISecretStore, CredentialManagerSecretStore>();
        builder.Services.AddSingleton(new AppInfo(version));
        builder.Services.AddPokeArenaGame(Path.Combine(AppContext.BaseDirectory, "data"));
        builder.Services.AddPokeArenaChat(twitchOptions);
        builder.Services.AddPokeArenaWebsite(websiteOptions);
        builder.Services.AddSingleton(overlayOptions);
        builder.Services.AddSingleton<OverlayStatus>();
        builder.Services.AddHostedService<OverlayServer>();
        builder.Services.AddSingleton<UpdateCheck>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<UpdateCheck>());
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        await _host.StartAsync();
        Log.Information(
            "PokeArena {Version} started, data in {DataDirectory}, channel {Channel}",
            version,
            paths.Root,
            string.IsNullOrWhiteSpace(twitchOptions.Channel) ? "(none)" : twitchOptions.Channel);

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Closing += OnMainWindowClosing;
        MainWindow = window;
        window.Show();
    }

    /// <summary>
    /// Stops the host before the window goes away: <c>OnExit</c> runs while the dispatcher shuts down, so an
    /// <c>await</c> there never continues — the chat queue would not be sent and the log not flushed (review 1.0.3).
    /// </summary>
    private async void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_hostStopped || _host is null)
        {
            return;
        }

        e.Cancel = true;
        if (_stopping)
        {
            return;
        }

        _stopping = true;
        ((Window)sender!).Hide();
        Log.Information("PokeArena shutting down");
        try
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Stopping PokeArena failed");
        }

        _hostStopped = true;

        // Close the window first, so the BlazorWebView goes while its services still exist; then dispose the host
        // with await while the dispatcher still runs — a synchronous Dispose in OnExit could deadlock on the overlay
        // server's async disposal and leave a hidden process holding the mutex (R1 second review).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ((Window)sender!).Close();
        try
        {
            if (_host is IAsyncDisposable host)
            {
                await host.DisposeAsync();
            }
            else
            {
                _host.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Disposing PokeArena failed");
        }

        _host = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // A host still here was not stopped through the window (Windows logs off and ignores e.Cancel): the process
        // ends anyway, so it is not disposed synchronously on the shutting-down dispatcher.
        Log.CloseAndFlush();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// The streamer's <c>settings.json</c> from the data directory (WP-08): above the shipped <c>appsettings.json</c>,
    /// below the developer's <c>appsettings.local.json</c> and the command line.
    /// </summary>
    private static void AddStreamerSettings(ConfigurationManager configuration, SettingsFile settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settings.Path)!);
        var source = new JsonConfigurationSource { Path = settings.Path, Optional = true };
        source.ResolveFileProvider();
        var sources = (IList<IConfigurationSource>)configuration.Sources;
        var local = sources.TakeWhile(s => s is not JsonConfigurationSource { Path: "appsettings.local.json" }).Count();
        sources.Insert(local, source);
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Serilog directly: these may fire before the host and its loggers exist.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception (terminating: {IsTerminating})", args.IsTerminating);

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception on UI thread");
        MessageBox.Show($"Ein unerwarteter Fehler ist aufgetreten:\n\n{e.Exception.Message}", "PokeArena", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
