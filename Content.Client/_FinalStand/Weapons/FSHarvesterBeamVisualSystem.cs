using System.Numerics;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Animations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Weapons;

// Draws the Harvester's own muzzle/travel/impact flashes instead of going through the shared vanilla hitscan effect pipeline.
public sealed class FSHarvesterBeamVisualSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly AnimationPlayerSystem _animPlayer = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;

    private static readonly EntProtoId HitscanProto = "HitscanEffect";
    private static readonly Color BeamColor = Color.FromHex("#AA44FF");
    private static readonly TimeSpan EffectLifetime = TimeSpan.FromSeconds(0.15);

    private const string RsiPath = "/Textures/_FinalStand/Effects/Harvester.rsi";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSHarvesterBeamFiredEvent>(OnBeamFired);
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
    }

    private void SpawnFlash(EntityCoordinates origin, TransformComponent relativeXform, Angle angle,
        string state, float lengthScale, float offsetAlongBeam)
    {
        if (lengthScale <= 0f)
            return;

        var coords = origin.Offset(angle.ToVec() * offsetAlongBeam);
        var ent = Spawn(HitscanProto, coords);

        var light = EnsureComp<PointLightComponent>(ent);
        _lights.SetCastShadows(ent, false, light);
        _lights.SetColor(ent, BeamColor, light);
        _lights.SetRadius(ent, 2.5f, light);
        _lights.SetEnergy(ent, 6f, light);
        _lights.SetEnabled(ent, true, light);

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
