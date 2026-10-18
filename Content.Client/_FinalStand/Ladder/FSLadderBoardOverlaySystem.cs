using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Ladder;

public sealed partial class FSLadderBoardOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new FSLadderBoardOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSLadderBoardOverlay>();
    }
}
