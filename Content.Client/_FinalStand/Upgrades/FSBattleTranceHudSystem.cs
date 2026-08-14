using Content.Shared._FinalStand.Upgrades.Effects;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Upgrades;

public sealed partial class FSBattleTranceHudSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    private FSBattleTranceHudOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSBattleTranceStateEvent>(OnState);
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

    private void OnState(FSBattleTranceStateEvent ev)
    {
        _overlay ??= new FSBattleTranceHudOverlay();
        if (!_overlayManager.HasOverlay<FSBattleTranceHudOverlay>())
            _overlayManager.AddOverlay(_overlay);

        _overlay.Stacks    = ev.Stacks;
        _overlay.MaxStacks = ev.MaxStacks;
        _overlay.BonusPct  = ev.BonusPct;
    }
}
