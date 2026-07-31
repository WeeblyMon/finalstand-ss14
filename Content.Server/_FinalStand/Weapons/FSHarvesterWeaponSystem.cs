using System.Numerics;
using Content.Server._FinalStand.Research;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Weapons;

// Harvester-only hooks: the beam is faked out of many rapid discrete hitscan shots rather than a real continuous-damage weapon (see WeaponHarvesterFS).
public sealed class FSHarvesterWeaponSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly FSResearchSystem _research = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float BaseDamage = 2f;
    private const int RpPerHit = 5;
    private const float DefaultRange = 7f;

    private static readonly SoundSpecifier LoopSound =
        new SoundPathSpecifier("/Audio/_FinalStand/Weapons/Harvester/laser.ogg", AudioParams.Default.WithLoop(true).WithMaxDistance(12f));

    private static readonly TimeSpan StopAfter = TimeSpan.FromMilliseconds(150);

    private readonly Dictionary<EntityUid, (EntityUid Stream, TimeSpan LastShot)> _active = new();
    private EntityQuery<FSWeaponUpgradeStateComponent> _upgradeQuery;
    private EntityQuery<WaveSpawnedTagComponent> _enemyQuery;
    private EntityQuery<HitscanBasicRaycastComponent> _raycastQuery;

    public override void Initialize()
    {
        base.Initialize();
        _upgradeQuery = GetEntityQuery<FSWeaponUpgradeStateComponent>();
        _enemyQuery = GetEntityQuery<WaveSpawnedTagComponent>();
        _raycastQuery = GetEntityQuery<HitscanBasicRaycastComponent>();

        SubscribeLocalEvent<FSHarvesterComponent, AmmoShotEvent>(OnShot);
        SubscribeLocalEvent<FSHarvesterComponent, HitscanRaycastFiredEvent>(OnHit);
    }

    private void OnShot(EntityUid uid, FSHarvesterComponent comp, AmmoShotEvent args)
    {
        var now = _timing.CurTime;

        if (_active.TryGetValue(uid, out var existing) && Exists(existing.Stream))
        {
            _active[uid] = (existing.Stream, now);
            return;
        }

        var played = _audio.PlayPvs(LoopSound, uid);
        if (played is { } stream)
            _active[uid] = (stream.Entity, now);
    }

    private void OnHit(EntityUid uid, FSHarvesterComponent comp, ref HitscanRaycastFiredEvent args)
    {
        // Transform(Gun) is stale while the gun sits in a hand container - use the shooter's transform instead, matching vanilla hitscan.
        var shooterOrGun = args.Data.Shooter ?? args.Data.Gun;
        var fromCoords = Transform(shooterOrGun).Coordinates;
        var shotAngle = args.Data.ShotDirection.ToAngle();

        var distance = DefaultRange;
        if (_raycastQuery.TryGetComponent(uid, out var raycast))
            distance = raycast.MaxDistance;

        if (args.Data.HitEntity is { } target)
        {
            var fromMap = _transform.ToMapCoordinates(fromCoords);
            var targetMap = _transform.ToMapCoordinates(Transform(target).Coordinates);
            distance = (targetMap.Position - fromMap.Position).Length();
        }

        // Re-express relative to the grid, mirroring HitscanBasicRaycastSystem.FireEffects exactly.
        var fromXform = Transform(fromCoords.EntityId);
        var gridUid = fromXform.GridUid;
        if (gridUid != fromCoords.EntityId && TryComp(gridUid, out TransformComponent? gridXform))
        {
            var (_, gridRot, gridInvMatrix) = _transform.GetWorldPositionRotationInvMatrix(gridXform);
            var map = _transform.ToMapCoordinates(fromCoords);
            fromCoords = new EntityCoordinates(gridUid.Value, Vector2.Transform(map.Position, gridInvMatrix));
            shotAngle -= gridRot;
        }
        else
        {
            shotAngle -= _transform.GetWorldRotation(fromXform);
        }

        RaiseNetworkEvent(new FSHarvesterBeamFiredEvent(GetNetCoordinates(fromCoords), (float)shotAngle.Theta, distance),
            Filter.Pvs(fromCoords, entityMan: EntityManager));

        if (args.Data.HitEntity is not { } hit)
            return;

        var multiplier = _upgradeQuery.TryGetComponent(args.Data.Gun, out var upgrade)
            ? upgrade.DamageMultiplier
            : 1f;

        var dmg = new DamageSpecifier();
        dmg.DamageDict["Radiation"] = FixedPoint2.New(BaseDamage * multiplier);

        if (!_damageable.TryChangeDamage(hit, dmg, out var dealt, origin: args.Data.Shooter ?? args.Data.Gun))
            return;

        var damageEvent = new HitscanDamageDealtEvent { Target = hit, DamageDealt = dealt };
        RaiseLocalEvent(uid, ref damageEvent);

        if (_enemyQuery.HasComponent(hit))
            _research.GrantResearchPoints(RpPerHit, "harvester-hit");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_active.Count == 0)
            return;

        var now = _timing.CurTime;
        List<EntityUid>? stale = null;
        foreach (var (gunUid, data) in _active)
        {
            if (now - data.LastShot < StopAfter)
                continue;

            _audio.Stop(data.Stream);
            stale ??= new List<EntityUid>();
            stale.Add(gunUid);
        }

        if (stale == null)
            return;

        foreach (var gunUid in stale)
            _active.Remove(gunUid);
    }
}
