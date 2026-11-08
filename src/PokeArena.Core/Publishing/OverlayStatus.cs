namespace PokeArena.Core.Publishing;

/// <summary>
/// Whether the OBS overlay could start. The desktop's overlay server reports a taken port here, so the overlay card
/// can say so instead of explaining a URL that shows nothing (review 1.0.3, A30).
/// </summary>
public sealed class OverlayStatus
{
    private volatile bool _portInUse;

    /// <summary>Raised after a change, on the reporting thread.</summary>
    public event Action? Changed;

    /// <summary>The configured port was taken by another program; the overlay is off until the next start.</summary>
    public bool PortInUse => _portInUse;

    public void ReportPortInUse()
    {
        _portInUse = true;
        Changed?.Invoke();
    }
}
