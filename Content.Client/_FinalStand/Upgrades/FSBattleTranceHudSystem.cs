using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Shared.Player;

namespace Content.Client._FinalStand.Upgrades;

public sealed partial class FSBattleTranceHudSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    private FSBattleTranceHudOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSBattleTranceStateEvent>(OnState);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        RemoveOverlay();
    }

    private void OnState(FSBattleTranceStateEvent ev)
    {
        if (ev.Stacks <= 0)
        {
            RemoveOverlay();
            return;
        }

        _overlay ??= new FSBattleTranceHudOverlay();
        if (!_overlayManager.HasOverlay<FSBattleTranceHudOverlay>())
            _overlayManager.AddOverlay(_overlay);

        _overlay.Stacks    = ev.Stacks;
        _overlay.MaxStacks = ev.MaxStacks;
        _overlay.BonusPct  = ev.BonusPct;
    }

    // The server can only zero the HUD by messaging the shooter, and on a round restart that
    // entity is already deleted — so the client has to clear itself or the counter survives into
    // the next round showing a buff that no longer exists.
    private void OnRoundRestart(RoundRestartCleanupEvent ev) => RemoveOverlay();

    private void OnLocalPlayerAttached(LocalPlayerAttachedEvent ev) => RemoveOverlay();

    private void RemoveOverlay()
    {
        if (_overlay == null)
            return;

        _overlayManager.RemoveOverlay(_overlay);
        _overlay.Stacks = 0;
        _overlay.BonusPct = 0;
        _overlay = null;
    }
}
