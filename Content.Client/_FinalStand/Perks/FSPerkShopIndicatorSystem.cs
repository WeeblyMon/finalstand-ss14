using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Perks;

public sealed class FSPerkShopIndicatorSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    private FSPerkShopIndicatorOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSPerkShopIndicatorOverlay();
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
