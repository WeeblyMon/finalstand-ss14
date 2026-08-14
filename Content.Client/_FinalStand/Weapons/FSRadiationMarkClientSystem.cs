using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Weapons;

public sealed partial class FSRadiationMarkClientSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    private FSRadiationMarkOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSRadiationMarkOverlay(EntityManager);
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
