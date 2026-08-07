using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Utility;
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Upgrades;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.Upgrades.Effects;

// on kill, stuns all wave enemies within 3 tiles for 0.3 seconds
public sealed class AftershockUpgradeSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;


    private readonly ObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>> _entSetPool =
        new DefaultObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>>(
            new SetPolicy<Entity<WaveSpawnedTagComponent>>());

    private const float StunRadius = 3f;
    private static readonly TimeSpan StunDuration = TimeSpan.FromSeconds(0.3);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
        SubscribeLocalEvent<FSAftershockTrackerComponent, MobStateChangedEvent>(OnKill);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (ev.State is not { } state || !state.AftershockEnabled)
            return;
        if (!HasComp<WaveSpawnedTagComponent>(ev.Target))
            return;

        var tracker = EnsureComp<FSAftershockTrackerComponent>(ev.Target);
        tracker.Weapon = ev.Weapon;
        tracker.Shooter = ev.Shooter;
    }

    private void OnKill(EntityUid uid, FSAftershockTrackerComponent tracker, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;
        if (tracker.Weapon is not { } gun || !Exists(gun))
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(gun, out var state) || !state.AftershockEnabled)
            return;

        var killPos = _xform.GetWorldPosition(uid);
        var mapId = Transform(uid).MapID;
        var nearby = _entSetPool.Get();
        _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(new MapCoordinates(killPos, mapId), StunRadius, nearby);

        foreach (var target in nearby)
        {
            if (target.Owner == uid || _mobState.IsDead(target.Owner))
                continue;
            _stun.TryAddStunDuration(target.Owner, StunDuration);
        }
        _entSetPool.Return(nearby);
    }
}
