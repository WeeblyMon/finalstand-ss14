using Content.Shared._FinalStand.Melee;
using Content.Shared._FinalStand.Perks;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Melee;

public sealed partial class FSObjectiveVignetteSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private const float FadeRate = 2f;
    private const string BunkeredDown = "BunkeredDown";

    private FSObjectiveVignetteOverlay _overlay = default!;
    private bool _bunkered;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSObjectiveVignetteOverlay(EntityManager, _player, _prototypes);
        _overlays.AddOverlay(_overlay);
        SubscribeNetworkEvent<FSPerkStacksUpdateEvent>(OnPerkStacks);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _bunkered = false);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }

    private void OnPerkStacks(FSPerkStacksUpdateEvent ev)
    {
        if (ev.PerkId == BunkeredDown)
            _bunkered = ev.Stacks > 0;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var target = 0f;
        var query = EntityQueryEnumerator<FSObjectiveVignetteComponent>();
        if (_bunkered && query.MoveNext(out var vignette))
        {
            target = 1f;
            _overlay.Color = vignette.Color;
            _overlay.Strength = vignette.Strength;
        }

        var step = FadeRate * frameTime;
        _overlay.Level = Math.Clamp(target, _overlay.Level - step, _overlay.Level + step);
    }
}
