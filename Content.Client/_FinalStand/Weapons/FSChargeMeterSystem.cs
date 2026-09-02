using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Weapons;

public sealed partial class FSChargeMeterSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    private FSChargeMeterOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSChargeMeterOverlay();
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
