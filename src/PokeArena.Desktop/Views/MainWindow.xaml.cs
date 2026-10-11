using System.Windows;
using PokeArena.Desktop.Services;

namespace PokeArena.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow(IServiceProvider services)
    {
        WindowTheme.PrepareWebViewBackground();
        InitializeComponent();

        WebView.Services = services;
        WindowTheme.Attach(this);
    }
}
