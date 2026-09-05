using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Loot;

public sealed class FSGachaLabelSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    private FSGachaLabelOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSGachaLabelOverlay();
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
