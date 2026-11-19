using Microsoft.Extensions.DependencyInjection;
using PokeArena.Core.Publishing;

namespace PokeArena.UI.Tests;

public class OverlayPanelTests : BunitContext
{
    private readonly OverlayStatus _status = new();

    public OverlayPanelTests()
    {
        Services.AddSingleton(new OverlayOptions());
        Services.AddSingleton(_status);
    }

    [Fact]
    public void Shows_the_url_and_how_to_reload_obs()
    {
        var cut = Render<OverlayPanel>();

        Assert.Equal("http://localhost:5299/overlay", cut.Find("[data-testid=overlay-url]").TextContent);
        Assert.Contains("Starte PokeArena vor OBS", cut.Markup);
    }

    [Fact]
    public void A_taken_port_is_reported_instead_of_the_url()
    {
        var cut = Render<OverlayPanel>();

        _status.ReportPortInUse();

        cut.WaitForAssertion(() => Assert.Contains("konnte nicht starten", cut.Find("[data-testid=overlay-problem]").TextContent));
        Assert.Empty(cut.FindAll("[data-testid=overlay-url]"));
    }

    [Fact]
    public void Another_start_error_is_reported_without_blaming_another_program()
    {
        var cut = Render<OverlayPanel>();

        _status.ReportFailed();

        cut.WaitForAssertion(() => Assert.Contains("Der Bot läuft trotzdem", cut.Find("[data-testid=overlay-problem]").TextContent));
        Assert.DoesNotContain("anderes Programm", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid=overlay-url]"));
    }
}
