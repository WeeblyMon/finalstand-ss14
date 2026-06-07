using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Augments;

public sealed class FSAugmentShopIndicatorSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    private FSAugmentShopIndicatorOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSAugmentShopIndicatorOverlay();
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
