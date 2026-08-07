using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Utility;
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Upgrades;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.Upgrades.Effects;

// on kill, spawns 3 explosion puffs and applies 50% damage to nearby enemies; kills chain
public sealed class PulseCascadeUpgradeSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;


    private readonly ObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>> _entSetPool =
        new DefaultObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>>(
            new SetPolicy<Entity<WaveSpawnedTagComponent>>());

    private const string CascadeExplosionProto = "FSPulseCascadeExplosion";
    private const int CascadeCount = 3;
    private const float CascadeRadius = 2f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
        SubscribeLocalEvent<FSPulseCascadeTrackerComponent, MobStateChangedEvent>(OnKill);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (ev.State is not { } state || !state.PulseCascadeEnabled)
            return;
        if (!HasComp<WaveSpawnedTagComponent>(ev.Target))
            return;

        var tracker = EnsureComp<FSPulseCascadeTrackerComponent>(ev.Target);
        tracker.Weapon = ev.Weapon;
        tracker.Shooter = ev.Shooter;
        tracker.Damage = ev.Damage;
    }

    private void OnKill(EntityUid uid, FSPulseCascadeTrackerComponent tracker, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;
        if (tracker.Weapon is not { } gun || !Exists(gun))
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(gun, out var state) || !state.PulseCascadeEnabled)
            return;
        if (tracker.Shooter is { } shooter && Exists(shooter) && !_hands.IsHolding(shooter, gun, out _))
            return;

        var killPos = _xform.GetWorldPosition(uid);
        var mapId = Transform(uid).MapID;
        var epicenter = new MapCoordinates(killPos, mapId);
        var cascadeDamage = tracker.Damage * FixedPoint2.New(0.5f);

        for (var i = 0; i < CascadeCount; i++)
        {
            _explosion.QueueExplosion(epicenter, CascadeExplosionProto,
                totalIntensity: 2f, slope: 5f, maxTileIntensity: 1f,
                cause: tracker.Shooter, tileBreakScale: 0f, maxTileBreak: 0,
                canCreateVacuum: false, addLog: false);
        }

        var nearby = _entSetPool.Get();
        _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(epicenter, CascadeRadius, nearby);

        foreach (var target in nearby)
        {
            if (target.Owner == uid || _mobState.IsDead(target.Owner))
                continue;

            var chainTracker = EnsureComp<FSPulseCascadeTrackerComponent>(target.Owner);
            chainTracker.Weapon = tracker.Weapon;
            chainTracker.Shooter = tracker.Shooter;
            chainTracker.Damage = cascadeDamage;

            _damageable.TryChangeDamage(target.Owner, cascadeDamage, ignoreResistances: false, origin: tracker.Shooter);
        }
        _entSetPool.Return(nearby);
    }
}
