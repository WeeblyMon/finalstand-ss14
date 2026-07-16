using Content.Shared._FinalStand.Weapons;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Upgrades;

public sealed class FSMarksmansRhythmHudSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    private FSMarksmansRhythmHudOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSMarksmansRhythmStateEvent>(OnState);
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

    private void OnState(FSMarksmansRhythmStateEvent ev)
    {
        _overlay ??= new FSMarksmansRhythmHudOverlay();
        if (!_overlayManager.HasOverlay<FSMarksmansRhythmHudOverlay>())
            _overlayManager.AddOverlay(_overlay);

        _overlay.Stacks    = ev.Stacks;
        _overlay.MaxStacks = ev.MaxStacks;
        _overlay.BonusPct  = ev.BonusPct;
    }
}
