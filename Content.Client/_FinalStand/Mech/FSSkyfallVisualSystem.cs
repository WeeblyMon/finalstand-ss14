// Fakes a Skyfall missile's arc: the missile layer climbs and grows over its shadow, then drops back as it dives. Leaves a smoke trail
// and marks what each missile has locked on to.
using System.Numerics;
using Content.Client._FinalStand.Particles;
using Content.Shared._FinalStand.Mech;
using Content.Shared._FinalStand.Particles;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Mech;

public sealed partial class FSSkyfallVisualSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private FSParticleSystem _particles = default!;
    [Dependency] private IOverlayManager _overlays = default!;

    private static readonly ProtoId<FSParticleEmitterPrototype> MissileTrail = "FSMissileTrail";

    private const string MissileLayer = "missile";
    private const float GrowBy = 0.5f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSSkyfallMissileComponent, ComponentStartup>((uid, _, _) => _particles.Attach(MissileTrail, uid));
        _overlays.AddOverlay(new FSSkyfallLockOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSSkyfallLockOverlay>();
    }

    public override void FrameUpdate(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSSkyfallMissileComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var missile, out var sprite, out var xform))
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), MissileLayer, out var layer, false))
                continue;

            var t = (float) (now - missile.Launched).TotalSeconds;
            float lift;
            if (t < 0f || missile.Launched == TimeSpan.Zero)
                lift = 0f;
            else if (t < missile.Ascend)
                lift = MathF.Sin(t / missile.Ascend * MathF.PI / 2f);
            else
                lift = MathF.Max(0f, 1f - (t - missile.Ascend) / missile.Dive);

            var up = (-_transform.GetWorldRotation(xform)).RotateVec(new Vector2(0f, missile.Height * lift));
            _sprite.LayerSetOffset((uid, sprite), layer, up);
            _sprite.LayerSetScale((uid, sprite), layer, new Vector2(1f + GrowBy * lift));
        }
    }
}
