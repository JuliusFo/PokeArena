using System.Windows;
using System.Windows.Controls;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using PokeArena.Desktop.Services;
using PokeArena.UI;
using Serilog;

namespace PokeArena.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private bool _webViewLoaded;
    private bool _closing;

    public MainWindow(IServiceProvider services)
    {
        _services = services;
        WindowTheme.PrepareWebViewBackground();
        InitializeComponent();

        Content = CreateWebView();
        WindowTheme.Attach(this);
    }

    /// <summary>The current WebView finished loading (it got its graphics device and browser).</summary>
    public event EventHandler? WebViewLoaded;

    /// <summary>
    /// Replaces the WebView after a failed load (no Direct3D device in a disconnected remote desktop session): the
    /// control never retries by itself, so the window would stay empty. One that never loaded is dropped, not
    /// disposed — its start waits for a device that never came, and disposing would wait on that too; one that had
    /// loaded is disposed first, so two browsers never run side by side.
    /// </summary>
    public async Task RebuildWebViewAsync()
    {
        try
        {
            if (Content is BlazorWebView old && _webViewLoaded)
            {
                Content = null;
                // A hanging disposal must not keep the window empty.
                await old.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        finally
        {
            // Closed meanwhile (the app is stopping its services): no new WebView against them.
            if (!_closing)
            {
                Content = CreateWebView();
            }
        }
    }

    /// <summary>Shown in place of the WebView when it could not load after several tries; the bot keeps running.</summary>
    public void ShowLoadFailedHint() =>
        Content = new TextBlock
        {
            // Color: inherited from the window's Foreground, which WindowTheme sets for light and dark.
            Text = "Die Anzeige konnte nicht geladen werden. Der Bot läuft trotzdem weiter.\nBitte neu verbinden oder PokeArena neu starten.",
            FontSize = 16,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24),
        };

    /// <summary>App cancels the first close to stop the host first, but the window never comes back.</summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _closing = true;
        base.OnClosing(e);
    }

    private BlazorWebView CreateWebView()
    {
        _webViewLoaded = false;
        var webView = new BlazorWebView { HostPage = @"wwwroot\index.html", Services = _services };
        webView.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = typeof(Shell) });
        webView.BlazorWebViewInitialized += (_, _) =>
        {
            if (ReferenceEquals(Content, webView))
            {
                _webViewLoaded = true;
                WebViewLoaded?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Replaced before it finished (its device came after all, but the retry was quicker — the WebView2 start
            // takes seconds): dispose it, so no second browser and Blazor UI run unseen beside the new one.
            _ = DisposeReplacedAsync(webView);
        };
        return webView;
    }

    private static async Task DisposeReplacedAsync(BlazorWebView webView)
    {
        try
        {
            Log.Information("Disposing a replaced window content that finished loading late");
            await webView.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Disposing a replaced window content failed");
        }
    }
}
