using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Ammo;

public sealed partial class WaveAmmoBoxIndicatorSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    private WaveAmmoBoxIndicatorOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new WaveAmmoBoxIndicatorOverlay();
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
}
