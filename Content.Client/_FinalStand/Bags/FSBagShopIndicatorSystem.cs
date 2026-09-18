using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Bags;

public sealed partial class FSBagShopIndicatorSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    private FSBagShopIndicatorOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSBagShopIndicatorOverlay();
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
