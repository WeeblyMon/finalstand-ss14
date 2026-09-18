using Robust.Client.Graphics;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSSyringeFillerIndicatorSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    private FSSyringeFillerIndicatorOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSSyringeFillerIndicatorOverlay();
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay == null)
            return;

        _overlayManager.RemoveOverlay(_overlay);
        _overlay = null;
    }
}
