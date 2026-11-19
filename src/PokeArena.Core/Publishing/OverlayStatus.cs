namespace PokeArena.Core.Publishing;

/// <summary>
/// Whether the OBS overlay could start. The desktop's overlay server reports a failed start here, so the overlay card
/// can say so instead of explaining a URL that shows nothing (review 1.0.3, A30).
/// </summary>
public sealed class OverlayStatus
{
    private volatile bool _portInUse;
    private volatile bool _failed;

    /// <summary>Raised after a change, on the reporting thread.</summary>
    public event Action? Changed;

    /// <summary>The configured port was taken by another program; the overlay is off until the next start.</summary>
    public bool PortInUse => _portInUse;

    /// <summary>The overlay could not start for another reason (e.g. a port Windows reserved); off until the next start.</summary>
    public bool Failed => _failed;

    public void ReportPortInUse()
    {
        _portInUse = true;
        Changed?.Invoke();
    }

    public void ReportFailed()
    {
        _failed = true;
        Changed?.Invoke();
    }
}
