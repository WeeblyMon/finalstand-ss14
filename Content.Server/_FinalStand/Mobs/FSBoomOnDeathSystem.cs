using System.Numerics;
using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Utility;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Ghost.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Mobs;

public sealed partial class FSBoomOnDeathSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FSDamageVulnerabilitySystem _vulnerability = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float ProximityInterval = 0.25f;

    private readonly ObjectPool<HashSet<Entity<ActorComponent>>> _actorSetPool =
        new DefaultObjectPool<HashSet<Entity<ActorComponent>>>(
            new SetPolicy<Entity<ActorComponent>>());

    private readonly ObjectPool<HashSet<Entity<DamageableComponent>>> _damageableSetPool =
        new DefaultObjectPool<HashSet<Entity<DamageableComponent>>>(
            new SetPolicy<Entity<DamageableComponent>>());

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSBoomOnDeathComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Exploded)
                continue;

            // Proximity countdown in progress
            if (comp.PendingExplosion)
            {
                comp.ExplosionTimer -= frameTime;
                if (comp.ExplosionTimer > 0f)
                    continue;
                comp.Exploded = true;
                comp.PendingExplosion = false;
                if (TryComp<FSBloaterFlashingComponent>(uid, out _))
                    RemComp<FSBloaterFlashingComponent>(uid);
                Explode(uid, comp);
                QueueDel(uid);
                continue;
            }

            if (!TryComp<MobStateComponent>(uid, out var mobState))
                continue;

            // Killed by damage → immediate explosion, no warning
            if (mobState.CurrentState == MobState.Dead)
            {
                comp.Exploded = true;
                if (TryComp<FSBloaterFlashingComponent>(uid, out _))
                    RemComp<FSBloaterFlashingComponent>(uid);
                Explode(uid, comp);
                continue;
            }

            // Alive — check if player within proximity range
            if (comp.ProximityRange <= 0f)
                continue;

            comp.ProximityAccumulator += frameTime;
            if (comp.ProximityAccumulator < ProximityInterval)
                continue;
            comp.ProximityAccumulator = 0f;

            var worldPos = _transform.GetWorldPosition(uid);
            var mapId = Transform(uid).MapID;
            var epicenter = new MapCoordinates(worldPos, mapId);
            var players = _actorSetPool.Get();
            _lookup.GetEntitiesInRange<ActorComponent>(epicenter, comp.ProximityRange, players);

            foreach (var (playerUid, _) in players)
            {
                if (HasComp<WaveSpawnedTagComponent>(playerUid))
                    continue;
                if (HasComp<GhostComponent>(playerUid))
                    continue;

                EnsureComp<FSBloaterFlashingComponent>(uid);
                comp.PendingExplosion = true;
                comp.ExplosionTimer = comp.FlashCount * comp.FlashInterval * 2f;
                break;
            }
            _actorSetPool.Return(players);
        }
    }

    private void Explode(EntityUid uid, FSBoomOnDeathComponent comp)
    {
        var coords = Transform(uid).Coordinates;
        var worldPos = _transform.GetWorldPosition(uid);
        var mapId = Transform(uid).MapID;
        var epicenter = new MapCoordinates(worldPos, mapId);

        var r = (int)comp.ExplosionRadius;
        for (var x = -r; x <= r; x++)
        {
            for (var y = -r; y <= r; y++)
            {
                if (x * x + y * y > comp.ExplosionRadius * comp.ExplosionRadius)
                    continue;
                Spawn("FSBloaterExplosionEffect", coords.Offset(new Vector2(x, y)));
            }
        }

        _audio.PlayPvs(comp.ExplosionSound, coords, comp.ExplosionSound.Params);

        var blastDamage = new DamageSpecifier();
        blastDamage.DamageDict["Blunt"] = FixedPoint2.New(comp.ExplosionDamage);
        blastDamage.DamageDict["Toxin"] = FixedPoint2.New(comp.ToxinDamage);

        // One query at the wider radius; each effect then filters by its own.
        var searchRadius = MathF.Max(comp.ExplosionRadius, comp.SlowRadius);
        var inRange = _damageableSetPool.Get();
        _lookup.GetEntitiesInRange<DamageableComponent>(epicenter, searchRadius, inRange);

        foreach (var (targetUid, _) in inRange)
        {
            if (targetUid == uid) continue;
            if (HasComp<WaveSpawnedTagComponent>(targetUid)) continue;
            if (HasComp<GhostComponent>(targetUid)) continue;
            if (!HasComp<MobStateComponent>(targetUid)) continue; // skip structures, walls, machines

            var dist = Vector2.Distance(worldPos, _transform.GetWorldPosition(targetUid));

            if (dist <= comp.ExplosionRadius)
            {
                _damageable.TryChangeDamage(targetUid, blastDamage, ignoreResistances: false, origin: uid);
                _vulnerability.Apply(targetUid, duration: 5f);
            }

            if (comp.SlowRadius > 0f && dist <= comp.SlowRadius)
            {
                var slow = EnsureComp<FSSlowedComponent>(targetUid);
                slow.EndTime = _timing.CurTime + TimeSpan.FromSeconds(comp.SlowDuration);
                slow.SlowFactor = comp.SlowAmount;
                _movement.RefreshMovementSpeedModifiers(targetUid);
            }
        }
        _damageableSetPool.Return(inRange);
    }
}
