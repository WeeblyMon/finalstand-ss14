using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Utility;
using Content.Server._FinalStand.Effects;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed partial class BeamChainingUpgradeSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private FSFlairSystem _flair = default!;


    private readonly ObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>> _entSetPool =
        new DefaultObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>>(
            new SetPolicy<Entity<WaveSpawnedTagComponent>>());

    // Reused across hits; the damage below cannot re-enter this handler.
    private readonly List<(EntityUid Uid, float DistSq)> _candidates = new();

    private const float ChainRange = 5f;
    private static readonly EntProtoId ChainBeamSegment = "FSChainBeamSegment";

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
            // Sprite-only: BeamSystem spawns physics entities that cancel primary damage.
            _flair.Beam(ev.Target, _candidates[i].Uid, ChainBeamSegment);
        }
    }
}
