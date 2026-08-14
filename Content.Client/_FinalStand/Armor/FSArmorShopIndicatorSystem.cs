using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Armor;

public sealed partial class FSArmorShopIndicatorSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    private FSArmorShopIndicatorOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSArmorShopIndicatorOverlay();
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
