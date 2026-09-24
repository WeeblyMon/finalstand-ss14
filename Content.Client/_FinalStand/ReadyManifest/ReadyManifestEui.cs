// Client half of the lobby Ready Manifest window, ported from Moffstation.
using Content.Client.Eui;
using Content.Shared._FinalStand.ReadyManifest;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._FinalStand.ReadyManifest;

[UsedImplicitly]
public sealed class ReadyManifestEui : BaseEui
{
    private readonly ReadyManifestUi _window;

    public ReadyManifestEui()
    {
        _window = new ReadyManifestUi();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is ReadyManifestEuiState cast)
            _window.RebuildUI(cast.JobCounts);
    }
}
