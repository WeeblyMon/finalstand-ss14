using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Weapons;

public sealed class FSGravitonCoreSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<FSTeslaArcComponent> _arcQuery;
    private EntityQuery<MobStateComponent> _mobQuery;

    public override void Initialize()
    {
        base.Initialize();
        _arcQuery = GetEntityQuery<FSTeslaArcComponent>();
        _mobQuery = GetEntityQuery<MobStateComponent>();
        SubscribeLocalEvent<FSGravitonCoreComponent, GunShotEvent>(OnGravitonShot);
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, GunShotEvent>(OnUpgradeStateShot);
    }

    private void OnGravitonShot(EntityUid gunUid, FSGravitonCoreComponent core, ref GunShotEvent args)
    {
        foreach (var (orbUid, _) in args.Ammo)
        {
            if (orbUid == null || !_arcQuery.HasComponent(orbUid.Value))
                continue;

            var pull = EnsureComp<FSGravitonPullComponent>(orbUid.Value);
            pull.Strength = core.PullStrengthBase * core.Level;
            pull.Range = core.MaxRangeBase + core.Level;
        }
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime.TotalSeconds;
        var query = EntityQueryEnumerator<FSGravitonPullComponent>();
        while (query.MoveNext(out var orbUid, out var pull))
        {
            if (curTime < pull.NextPulseTime)
                continue;
            pull.NextPulseTime = curTime + pull.PulseInterval;

            var myPos = _transform.GetMapCoordinates(orbUid);
            if (myPos.MapId == MapId.Nullspace)
                continue;

            var targets = new HashSet<Entity<WaveSpawnedTagComponent>>();
            _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(myPos, pull.Range, targets);

            foreach (var (targetUid, _) in targets)
            {
                if (!_mobQuery.TryGetComponent(targetUid, out var ms) || ms.CurrentState != MobState.Alive)
                    continue;

                var targetPos = _transform.GetMapCoordinates(targetUid);
                var dir = myPos.Position - targetPos.Position;
                if (dir.LengthSquared() < 0.01f)
                    continue;

                _physics.ApplyLinearImpulse(targetUid, Vector2.Normalize(dir) * pull.Strength);
            }
        }
    }

    private void OnUpgradeStateShot(EntityUid gunUid, FSWeaponUpgradeStateComponent state, ref GunShotEvent args)
    {
        if (state.TeslaArcRangeBonus <= 0f)
            return;

        foreach (var (orbUid, _) in args.Ammo)
        {
            if (orbUid == null)
                continue;
            if (!_arcQuery.TryGetComponent(orbUid.Value, out var arc))
                continue;

            arc.ArcRange += state.TeslaArcRangeBonus;
        }
    }
}
