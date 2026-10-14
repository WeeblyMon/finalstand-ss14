using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Repair;

public sealed partial class FSStructureHealthOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new FSStructureHealthOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSStructureHealthOverlay>();
    }
}
