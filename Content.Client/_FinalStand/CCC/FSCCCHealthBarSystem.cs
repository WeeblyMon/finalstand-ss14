using Content.Shared._FinalStand.CCC;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.CCC;

public sealed partial class FSCCCHealthBarSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    private FSCCCHealthBarOverlay? _overlay;
    private readonly FSCCCLifeLostOverlay _lifeLost = new();

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSCCCHealthBarOverlay(EntityManager);
        _overlayManager.AddOverlay(_overlay);
        _overlayManager.AddOverlay(_lifeLost);
        SubscribeNetworkEvent<FSCCCLifeLostEvent>(ev => _lifeLost.Show(ev.LivesLeft, ev.Lives));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay(_lifeLost);
        if (_overlay != null)
        {
            _overlayManager.RemoveOverlay(_overlay);
            _overlay = null;
        }
    }
}
