using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Objectives;

public sealed partial class FSObjectiveMarkerSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new FSObjectiveMarkerOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSObjectiveMarkerOverlay>();
    }
}
