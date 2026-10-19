using System.Numerics;
using Content.Server._FinalStand.Effects;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Weapons;

public sealed partial class FSTeslaArcSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private FSFlairSystem _flair = default!;

    private static readonly EntProtoId BeamSegment = "FSTeslaGunBeamSegment";
    private const string HitEffect = "FSTeslaGunHitEffect";

    private EntityQuery<FSFriendlyFireComponent> _ffQuery;
    private EntityQuery<WaveSpawnedTagComponent> _waveQuery;
    private EntityQuery<MobStateComponent> _mobQuery;

    private readonly HashSet<Entity<WaveSpawnedTagComponent>> _candidates = new();

    public override void Initialize()
    {
        base.Initialize();
        _ffQuery = GetEntityQuery<FSFriendlyFireComponent>();
        _waveQuery = GetEntityQuery<WaveSpawnedTagComponent>();
        _mobQuery = GetEntityQuery<MobStateComponent>();
        SubscribeLocalEvent<FSTeslaArcComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, FSTeslaArcComponent comp, ComponentStartup args)
    {
        comp.NextArcTime = _timing.CurTime.TotalSeconds;
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime.TotalSeconds;
        var query = EntityQueryEnumerator<FSTeslaArcComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (curTime < comp.NextArcTime)
                continue;
            comp.NextArcTime = curTime + comp.ArcInterval;
            if (comp.Shooter == null && TryComp<ProjectileComponent>(uid, out var proj))
                comp.Shooter = proj.Shooter;
            FireArcs(uid, comp);
            comp.TotalArcsFired++;
            if (comp.TotalArcsFired >= comp.MaxTotalArcs)
                QueueDel(uid);
        }
    }

    private void FireArcs(EntityUid uid, FSTeslaArcComponent comp)
    {
        var myPos = _transform.GetMapCoordinates(uid);
        _candidates.Clear();
        _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(myPos, comp.ArcRange, _candidates);

        var arcsLeft = comp.MaxArcs;
        foreach (var (targetUid, _) in _candidates)
        {
            if (arcsLeft <= 0) break;
            if (!_mobQuery.TryGetComponent(targetUid, out var mobState)) continue;
            if (mobState.CurrentState != MobState.Alive) continue;
            if (_ffQuery.HasComponent(targetUid)) continue;

            _damageable.TryChangeDamage(targetUid, comp.Damage, ignoreResistances: false, origin: comp.Shooter ?? uid);
            _flair.Beam(uid, targetUid, BeamSegment);
            Spawn(HitEffect, new EntityCoordinates(targetUid, Vector2.Zero));
            arcsLeft--;
        }
    }
}
