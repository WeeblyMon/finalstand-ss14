using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

public sealed partial class FSMediGunSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IResourceCache _resourceCache = default!;

    private FSMediGunBeamOverlay? _overlay;
    private FSReviveIndicatorOverlay? _reviveOverlay;
    private FSDartTracerOverlay? _tracerOverlay;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new FSMediGunBeamOverlay(EntityManager, _timing, _resourceCache);
        _overlayManager.AddOverlay(_overlay);

        _reviveOverlay = new FSReviveIndicatorOverlay(EntityManager, _timing, _resourceCache);
        _overlayManager.AddOverlay(_reviveOverlay);

        _tracerOverlay = new FSDartTracerOverlay(EntityManager);
        _overlayManager.AddOverlay(_tracerOverlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_overlay != null)
        {
            _overlayManager.RemoveOverlay(_overlay);
            _overlay = null;
        }

        if (_reviveOverlay != null)
        {
            _overlayManager.RemoveOverlay(_reviveOverlay);
            _reviveOverlay = null;
        }

        if (_tracerOverlay != null)
        {
            _overlayManager.RemoveOverlay(_tracerOverlay);
            _tracerOverlay = null;
        }
    }
}
