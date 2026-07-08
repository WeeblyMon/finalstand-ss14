using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._FinalStand.Mobs;

public sealed class FSLectorExecutionSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private FSLectorExecutionOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSLectorExecutionOverlay(EntityManager, _player);
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
