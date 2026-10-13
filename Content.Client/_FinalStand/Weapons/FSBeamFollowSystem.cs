using System.Numerics;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;

namespace Content.Client._FinalStand.Weapons;

public sealed partial class FSBeamFollowSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const float MuzzleDistance = 0.5f;
    private const float TravelPadding = 0.5f;
    private const float TravelTrim = 1.5f;
    private const float MinLength = 0.01f;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<FSBeamFollowComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var beam, out var sprite, out var xform))
        {
            if (!Exists(beam.Shooter) || TerminatingOrDeleted(beam.Shooter))
                continue;

            if (!beam.Impact.IsValid(EntityManager))
                continue;

            var impact = _xform.ToMapCoordinates(beam.Impact);
            var origin = _xform.GetMapCoordinates(beam.Shooter);
            if (impact.MapId != origin.MapId || impact.MapId != xform.MapID)
                continue;

            var delta = impact.Position - origin.Position;
            var distance = delta.Length();
            if (distance < MinLength)
                continue;

            var dir = delta / distance;
            var angle = dir.ToAngle();

            switch (beam.Part)
            {
                case SharedGunSystem.HitscanPart.Muzzle:
                    _xform.SetWorldPositionRotation(uid, origin.Position + dir * MuzzleDistance, angle, xform);
                    break;

                case SharedGunSystem.HitscanPart.Travel:
                    _xform.SetWorldPositionRotation(uid,
                        origin.Position + dir * ((distance + TravelPadding) / 2f),
                        angle,
                        xform);
                    _sprite.SetScale((uid, sprite), new Vector2(MathF.Max(distance - TravelTrim, MinLength), 1f));
                    break;

                case SharedGunSystem.HitscanPart.Impact:
                    _xform.SetWorldPositionRotation(uid, impact.Position, angle.FlipPositive(), xform);
                    break;
            }
        }
    }
}
