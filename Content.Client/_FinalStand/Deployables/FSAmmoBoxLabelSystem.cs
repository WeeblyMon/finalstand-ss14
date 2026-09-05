using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Deployables;

public sealed partial class FSAmmoBoxLabelSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    private FSAmmoBoxLabelOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSAmmoBoxLabelOverlay();
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
