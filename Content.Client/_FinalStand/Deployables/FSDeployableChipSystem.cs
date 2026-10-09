using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Deployables;

public sealed class FSDeployableChipSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new FSDeployableChipOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSDeployableChipOverlay>();
    }
}
