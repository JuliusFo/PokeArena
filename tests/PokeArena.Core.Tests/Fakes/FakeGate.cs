namespace PokeArena.Core.Tests.Fakes;

/// <summary>When an update may install, as the test says. Shared with PokeArena.UI.Tests (linked there).</summary>
public sealed class FakeGate : IUpdateGate
{
    public bool Lobby { get; set; }
    public bool MayInstall { get; set; } = true;

    public bool LobbyOpen => Lobby;

    public bool MayInstallAutomatically => MayInstall && !Lobby;
}
