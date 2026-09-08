using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSBuffOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private FSBuffOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new FSBuffOverlay(EntityManager, _timing);
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_overlay != null)
            _overlayManager.RemoveOverlay(_overlay);

        _overlay = null;
    }
}
