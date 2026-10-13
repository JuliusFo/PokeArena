using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PokeArena.Core;
using PokeArena.Core.Game;
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
        var twitchOptions = builder.Configuration.GetSection(TwitchOptions.SectionName).Get<TwitchOptions>() ?? new();
        var paths = AppPaths.Resolve(options);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
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
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ISecretStore, CredentialManagerSecretStore>();
        builder.Services.AddSingleton(new AppInfo(version));
        builder.Services.AddPokeArenaGame(Path.Combine(AppContext.BaseDirectory, "data"));
        builder.Services.AddPokeArenaChat(twitchOptions);
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        await _host.StartAsync();
        Log.Information(
            "PokeArena {Version} started, data in {DataDirectory}, channel {Channel}",
            version,
            paths.Root,
            string.IsNullOrWhiteSpace(twitchOptions.Channel) ? "(none)" : twitchOptions.Channel);

        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("PokeArena shutting down");
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        await Log.CloseAndFlushAsync();
        _singleInstance?.Dispose();
        base.OnExit(e);
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
