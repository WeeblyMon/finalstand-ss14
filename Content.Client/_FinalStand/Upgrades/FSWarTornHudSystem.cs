using Content.Shared._FinalStand.Upgrades.Effects;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Upgrades;

public sealed class FSWarTornHudSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    private FSWarTornHudOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSWarTornStateEvent>(OnState);
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

    private void OnState(FSWarTornStateEvent ev)
    {
        _overlay ??= new FSWarTornHudOverlay();
        if (!_overlayManager.HasOverlay<FSWarTornHudOverlay>())
            _overlayManager.AddOverlay(_overlay);

        _overlay.Stacks    = ev.Stacks;
        _overlay.MaxStacks = ev.MaxStacks;
        _overlay.BonusPct  = ev.BonusPct;
    }
}
