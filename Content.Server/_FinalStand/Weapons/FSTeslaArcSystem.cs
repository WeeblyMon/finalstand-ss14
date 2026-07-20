using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Weapons;

public sealed class FSTeslaArcSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const string BeamSegment = "FSTeslaGunBeamSegment";
    private const string HitEffect = "FSTeslaGunHitEffect";

    private EntityQuery<FSFriendlyFireComponent> _ffQuery;
    private EntityQuery<WaveSpawnedTagComponent> _waveQuery;
    private EntityQuery<MobStateComponent> _mobQuery;

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
            FireArcs(uid, comp);
            comp.TotalArcsFired++;
            if (comp.TotalArcsFired >= comp.MaxTotalArcs)
                QueueDel(uid);
        }
    }

    private void FireArcs(EntityUid uid, FSTeslaArcComponent comp)
    {
        var myPos = _transform.GetMapCoordinates(uid);
        var candidates = new HashSet<Entity<WaveSpawnedTagComponent>>();
        _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(myPos, comp.ArcRange, candidates);

        var arcsLeft = comp.MaxArcs;
        foreach (var (targetUid, _) in candidates)
        {
            if (arcsLeft <= 0) break;
            if (!_mobQuery.TryGetComponent(targetUid, out var mobState)) continue;
            if (mobState.CurrentState != MobState.Alive) continue;
            if (_ffQuery.HasComponent(targetUid)) continue;

            _damageable.TryChangeDamage(targetUid, comp.Damage, ignoreResistances: false, origin: uid);
            DrawBeam(uid, targetUid);
            Spawn(HitEffect, new EntityCoordinates(targetUid, Vector2.Zero));
            arcsLeft--;
        }
    }

    private void DrawBeam(EntityUid from, EntityUid to)
    {
        var fromPos = _transform.GetMapCoordinates(from);
        var toPos = _transform.GetMapCoordinates(to);

        if (fromPos.MapId != toPos.MapId)
            return;

        var dir = toPos.Position - fromPos.Position;
        if (dir.LengthSquared() < 0.01f)
            return;

        var distance = dir.Length();
        var normalized = Vector2.Normalize(dir);
        var angle = dir.ToAngle();
        var segCount = Math.Max(1, (int) Math.Ceiling(distance));

        for (var i = 0; i < segCount; i++)
        {
            var pos = new MapCoordinates(fromPos.Position + normalized * i, fromPos.MapId);
            var seg = Spawn(BeamSegment, pos);
            _transform.SetWorldRotation(seg, angle);
        }
    }
}
