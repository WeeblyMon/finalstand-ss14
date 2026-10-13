using Content.Shared._FinalStand.Melee;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Melee;

public sealed partial class FSObjectiveVignetteSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private const float FadeRate = 2f;

    private FSObjectiveVignetteOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSObjectiveVignetteOverlay(EntityManager, _player, _prototypes);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var target = 0f;
        if (_player.LocalEntity is { } local)
        {
            var playerPos = _xform.GetMapCoordinates(local);
            var query = EntityQueryEnumerator<FSObjectiveVignetteComponent, TransformComponent>();
            while (query.MoveNext(out var vignette, out var xform))
            {
                if (xform.MapID != playerPos.MapId)
                    continue;

                var distance = (_xform.GetWorldPosition(xform) - playerPos.Position).Length();
                var level = Math.Clamp(1f - distance / vignette.Radius, 0f, 1f);
                if (level <= target)
                    continue;

                target = level;
                _overlay.Color = vignette.Color;
                _overlay.Strength = vignette.Strength;
            }
        }

        var step = FadeRate * frameTime;
        _overlay.Level = Math.Clamp(target, _overlay.Level - step, _overlay.Level + step);
    }
}
