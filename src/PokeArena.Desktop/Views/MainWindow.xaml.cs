using System.Windows;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using PokeArena.Desktop.Services;
using PokeArena.UI;

namespace PokeArena.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;

    public MainWindow(IServiceProvider services)
    {
        _services = services;
        WindowTheme.PrepareWebViewBackground();
        InitializeComponent();

        Content = CreateWebView();
        WindowTheme.Attach(this);
    }

    /// <summary>
    /// Replaces a WebView whose load failed (no Direct3D device in a disconnected remote desktop session): the
    /// control never retries by itself, so the window would stay empty. The failed one is dropped, not disposed —
    /// its start waits for a device that never came, and disposing would wait on that too.
    /// </summary>
    public void RebuildWebView() => Content = CreateWebView();

    private BlazorWebView CreateWebView()
    {
        var webView = new BlazorWebView { HostPage = @"wwwroot\index.html", Services = _services };
        webView.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = typeof(Shell) });
        return webView;
    }
}
