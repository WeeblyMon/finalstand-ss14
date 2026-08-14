using System.Numerics;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Animations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Weapons;

// Draws the Harvester's own muzzle/travel/impact flashes instead of going through the shared vanilla hitscan effect pipeline.
public sealed partial class FSHarvesterBeamVisualSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private AnimationPlayerSystem _animPlayer = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly EntProtoId HitscanProto = "HitscanEffect";
    private static readonly Color BeamColor = Color.FromHex("#AA44FF");
    private static readonly TimeSpan EffectLifetime = TimeSpan.FromSeconds(0.15);

    private const string RsiPath = "/Textures/_FinalStand/Effects/Harvester.rsi";

    // One light for the whole beam: the flashes spawn 60 a second.
    private static readonly TimeSpan GlowTimeout = TimeSpan.FromSeconds(0.2);
    private EntityUid? _glow;
    private TimeSpan _lastBeam;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSHarvesterBeamFiredEvent>(OnBeamFired);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_glow is not { } glow)
            return;
        if (_timing.CurTime - _lastBeam < GlowTimeout)
            return;

        if (Exists(glow))
            QueueDel(glow);
        _glow = null;
    }

    private void UpdateGlow(EntityCoordinates origin, Angle angle, float distance)
    {
        _lastBeam = _timing.CurTime;

        var midpoint = origin.Offset(angle.ToVec() * (distance * 0.5f));

        if (_glow is { } existing && Exists(existing))
        {
            _xform.SetCoordinates(existing, midpoint);
            return;
        }

        _glow = Spawn(null, midpoint);
        var light = EnsureComp<PointLightComponent>(_glow.Value);
        _lights.SetCastShadows(_glow.Value, false, light);
        _lights.SetColor(_glow.Value, BeamColor, light);
        _lights.SetRadius(_glow.Value, 4.5f, light);
        _lights.SetEnergy(_glow.Value, 7f, light);
        _lights.SetEnabled(_glow.Value, true, light);
    }

    private void OnBeamFired(FSHarvesterBeamFiredEvent ev)
    {
        var coords = GetCoordinates(ev.FromCoordinates);
        if (!TryComp(coords.EntityId, out TransformComponent? relativeXform))
            return;

        var angle = new Angle(ev.Angle);

        SpawnFlash(coords, relativeXform, angle, "muzzle_harvester_beam", 1f, 0.5f);

        if (ev.Distance >= 1f)
            SpawnFlash(coords, relativeXform, angle, "harvester_beam", ev.Distance - 1.5f, (ev.Distance + 0.5f) / 2f);

        // Impact uses the same angle as the beam - Angle.FlipPositive() only normalizes to a positive range, it does not reverse direction.
        SpawnFlash(coords, relativeXform, angle, "impact_harvester_beam", 1f, ev.Distance);

        UpdateGlow(coords, angle, ev.Distance);
    }

    private void SpawnFlash(EntityCoordinates origin, TransformComponent relativeXform, Angle angle,
        string state, float lengthScale, float offsetAlongBeam)
    {
        if (lengthScale <= 0f)
            return;

        var coords = origin.Offset(angle.ToVec() * offsetAlongBeam);
        var ent = Spawn(HitscanProto, coords);

        var sprite = Comp<SpriteComponent>(ent);
        var xform = Transform(ent);
        var targetWorldRot = angle + _xform.GetWorldRotation(relativeXform);
        var delta = targetWorldRot - _xform.GetWorldRotation(xform);
        _xform.SetLocalRotationNoLerp(ent, xform.LocalRotation + delta, xform);

        var rsi = new SpriteSpecifier.Rsi(new ResPath(RsiPath), state);
        sprite[EffectLayers.Unshaded].AutoAnimated = false;
        _sprite.LayerSetSprite((ent, sprite), EffectLayers.Unshaded, rsi);
        _sprite.LayerSetRsiState((ent, sprite), EffectLayers.Unshaded, state);
        _sprite.SetScale((ent, sprite), new Vector2(lengthScale, 1f));
        sprite[EffectLayers.Unshaded].Visible = true;

        var anim = new Animation
        {
            Length = EffectLifetime,
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = EffectLayers.Unshaded,
                    KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame(state, 0f) },
                },
            },
        };

        _animPlayer.Play(ent, anim, "harvester-beam-effect");
    }
}
