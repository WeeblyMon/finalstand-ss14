using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Utility;
using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed class BeamChainingUpgradeSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;


    private readonly ObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>> _entSetPool =
        new DefaultObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>>(
            new SetPolicy<Entity<WaveSpawnedTagComponent>>());

    // Reused across hits; the damage below cannot re-enter this handler.
    private readonly List<(EntityUid Uid, float DistSq)> _candidates = new();

    private const float ChainRange = 5f;
    private const string ChainBeamSegment = "FSChainBeamSegment";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (ev.State is not { } state || state.BeamChainTargets <= 0)
            return;

        var targetPos = _transform.GetWorldPosition(ev.Target);
        var mapId = Transform(ev.Target).MapID;

        var nearby = _entSetPool.Get();
        _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(new MapCoordinates(targetPos, mapId), ChainRange, nearby);

        _candidates.Clear();
        foreach (var candidate in nearby)
        {
            if (candidate.Owner == ev.Target || _mobState.IsDead(candidate.Owner))
                continue;
            var distSq = (_transform.GetWorldPosition(candidate.Owner) - targetPos).LengthSquared();
            _candidates.Add((candidate.Owner, distSq));
        }
        _entSetPool.Return(nearby);

        _candidates.Sort(static (a, b) => a.DistSq.CompareTo(b.DistSq));

        var chainCount = Math.Min(state.BeamChainTargets, _candidates.Count);
        for (var i = 0; i < chainCount; i++)
        {
            _damageable.TryChangeDamage(_candidates[i].Uid, ev.Damage, ignoreResistances: false, origin: ev.Shooter);
            DrawChainBeam(ev.Target, _candidates[i].Uid);
        }
    }

    // Physics-free sprites only — BeamSystem spawns physics entities that cancel primary damage.
    private void DrawChainBeam(EntityUid from, EntityUid to)
    {
        var fromMapCoords = _transform.GetMapCoordinates(from);
        var toMapCoords   = _transform.GetMapCoordinates(to);

        if (fromMapCoords.MapId != toMapCoords.MapId)
            return;

        var dir = toMapCoords.Position - fromMapCoords.Position;
        if (dir.LengthSquared() < 0.01f)
            return;

        var distance   = dir.Length();
        var normalized = Vector2.Normalize(dir);
        var angle      = dir.ToAngle();
        var segCount   = Math.Max(1, (int)Math.Ceiling(distance));

        for (var i = 0; i < segCount; i++)
        {
            var pos = new MapCoordinates(fromMapCoords.Position + normalized * i, fromMapCoords.MapId);
            var seg = Spawn(ChainBeamSegment, pos);
            _transform.SetWorldRotation(seg, angle);
        }
    }
}
