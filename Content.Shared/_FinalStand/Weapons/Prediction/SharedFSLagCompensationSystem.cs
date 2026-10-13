using System.Linq;
using System.Numerics;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._FinalStand.Weapons.Prediction;

public abstract partial class SharedFSLagCompensationSystem : EntitySystem
{
    [Dependency] protected SharedPhysicsSystem Physics = default!;

    protected bool RaisingPreventCollide;

    public virtual List<RayCastResults> IntersectRay(MapId mapId, CollisionRay ray, float maxLength, EntityUid? ignored, EntityUid? shooter)
    {
        return Physics.IntersectRay(mapId, ray, maxLength, ignored, false).ToList();
    }

    public virtual bool IsPredictedShot(EntityUid? shooter)
    {
        return false;
    }

    public bool CanPredictShots(EntityUid gun)
    {
        if (HasComp<FSChargeShotComponent>(gun))
            return false;

        return !TryComp<FSWeaponUpgradeStateComponent>(gun, out var state)
               || state.HomingLevel <= 0 && MathHelper.CloseTo(state.PelletSpreadMultiplier, 1f);
    }

    public bool CanPredictHitscan(EntityUid ammo)
    {
        return HasComp<HitscanBasicRaycastComponent>(ammo) && !HasComp<FSXrayRaycastComponent>(ammo);
    }

    public bool TryGetProjectileShape(EntityUid projectile, out Fixture fixture, out float halfWidth, out float reach)
    {
        halfWidth = 0f;
        reach = 0f;
        fixture = default!;

        if (!TryComp<FixturesComponent>(projectile, out var fixtures)
            || !fixtures.Fixtures.TryGetValue(SharedProjectileSystem.ProjectileFixture, out var found))
        {
            return false;
        }

        fixture = found;
        var box = found.Shape.ComputeAABB(new Transform(Vector2.Zero, 0f), 0);
        halfWidth = MathF.Min(box.Width, box.Height) / 2f;
        reach = MathF.Max(MathF.Max(MathF.Abs(box.Left), MathF.Abs(box.Right)), MathF.Max(MathF.Abs(box.Bottom), MathF.Abs(box.Top)));
        return true;
    }

    public bool TryGetBodyBounds(EntityUid uid, Vector2 position, int mask, out Box2 bounds, FixturesComponent? fixtures = null)
    {
        bounds = default;
        if (!Resolve(uid, ref fixtures, false))
            return false;

        var transform = new Transform(position, 0f);
        var found = false;

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard || (fixture.CollisionLayer & mask) == 0)
                continue;

            for (var i = 0; i < fixture.Shape.ChildCount; i++)
            {
                var aabb = fixture.Shape.ComputeAABB(transform, i);
                bounds = found ? bounds.Union(aabb) : aabb;
                found = true;
            }
        }

        return found;
    }

    public bool TryGetHitFixture(EntityUid uid, int mask, out string id, out Fixture fixture, FixturesComponent? fixtures = null)
    {
        id = string.Empty;
        fixture = default!;
        if (!Resolve(uid, ref fixtures, false))
            return false;

        foreach (var (fixtureId, candidate) in fixtures.Fixtures)
        {
            if (!candidate.Hard || (candidate.CollisionLayer & mask) == 0)
                continue;

            id = fixtureId;
            fixture = candidate;
            return true;
        }

        return false;
    }

    public bool IsCollisionPrevented(EntityUid a, PhysicsComponent bodyA, Fixture fixtureA, EntityUid b, PhysicsComponent bodyB, Fixture fixtureB)
    {
        RaisingPreventCollide = true;
        try
        {
            var ev = new PreventCollideEvent(a, b, bodyA, bodyB, fixtureA, fixtureB);
            RaiseLocalEvent(a, ref ev);
            if (ev.Cancelled)
                return true;

            var reverse = new PreventCollideEvent(b, a, bodyB, bodyA, fixtureB, fixtureA);
            RaiseLocalEvent(b, ref reverse);
            return reverse.Cancelled;
        }
        finally
        {
            RaisingPreventCollide = false;
        }
    }

    public static bool SegmentHitsBox(Vector2 start, Vector2 end, Box2 box, out float fraction)
    {
        fraction = 0f;
        if (box.Contains(start))
            return true;

        var delta = end - start;
        var min = 0f;
        var max = 1f;

        if (!Slab(start.X, delta.X, box.Left, box.Right, ref min, ref max)
            || !Slab(start.Y, delta.Y, box.Bottom, box.Top, ref min, ref max))
        {
            return false;
        }

        fraction = min;
        return true;
    }

    private static bool Slab(float origin, float delta, float low, float high, ref float min, ref float max)
    {
        if (MathF.Abs(delta) < 1e-6f)
            return origin >= low && origin <= high;

        var inverse = 1f / delta;
        var enter = (low - origin) * inverse;
        var exit = (high - origin) * inverse;
        if (enter > exit)
            (enter, exit) = (exit, enter);

        min = MathF.Max(min, enter);
        max = MathF.Min(max, exit);
        return min <= max;
    }
}
