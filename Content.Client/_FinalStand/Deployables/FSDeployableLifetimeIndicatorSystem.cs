using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Deployables;

public sealed class FSDeployableLifetimeIndicatorSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    private FSDeployableLifetimeOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSDeployableLifetimeOverlay();
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
