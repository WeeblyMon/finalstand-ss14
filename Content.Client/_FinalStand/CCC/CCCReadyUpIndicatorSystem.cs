using Content.Shared._FinalStand.WaveHud;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.CCC;

public sealed class CCCReadyUpIndicatorSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    private CCCReadyUpIndicatorOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<WavePhaseChangedEvent>(OnPhaseChanged);
        _overlay = new CCCReadyUpIndicatorOverlay();
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
        {
            _overlayManager.RemoveOverlay(_overlay);
            _overlay = null;
        }
    }

    private void OnPhaseChanged(WavePhaseChangedEvent ev)
    {
        if (_overlay != null)
            _overlay.ShowReminder = ev.IsPrepPhase;
    }
}
