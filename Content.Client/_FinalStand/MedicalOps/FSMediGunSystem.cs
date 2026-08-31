using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

public sealed partial class FSMediGunSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private FSMediGunBeamOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSMediGunBeamOverlay(EntityManager, _timing);
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_overlay == null)
            return;

        _overlayManager.RemoveOverlay(_overlay);
        _overlay = null;
    }
}
