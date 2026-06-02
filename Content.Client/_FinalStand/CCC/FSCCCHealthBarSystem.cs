using Robust.Client.Graphics;

namespace Content.Client._FinalStand.CCC;

public sealed class FSCCCHealthBarSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    private FSCCCHealthBarOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSCCCHealthBarOverlay(EntityManager);
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
