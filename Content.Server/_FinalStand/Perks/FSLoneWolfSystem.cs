using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSLoneWolfSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private FSPerkNotifySystem _notify = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextCheck;
    private TimeSpan _nextRegen;
    private readonly HashSet<EntityUid> _alone = [];
    private readonly HashSet<EntityUid> _wasAlone = [];
    private readonly HashSet<Entity<MindContainerComponent>> _nearby = [];

    public bool IsAlone(EntityUid body) => _alone.Contains(body);

    public float GetMultiplier(EntityUid body, FSPerkLevelsComponent perks)
    {
        var level = perks.GetSlottedLevel("LoneWolf");
        return level > 0 && _alone.Contains(body) ? 1f + level * FSPerkBonusConstants.LoneWolfDamagePerLevel : 1f;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now >= _nextCheck)
        {
            _nextCheck = now + CheckInterval;
            Recheck();
        }

        if (now < _nextRegen)
            return;
        _nextRegen = now + TimeSpan.FromSeconds(1);

        foreach (var body in _alone)
        {
            if (!_mind.TryGetMind(body, out var mindId, out _)
                || !TryComp<FSPerkLevelsComponent>(mindId, out var perks)
                || !_mobState.IsAlive(body))
                continue;

            var level = perks.GetSlottedLevel("LoneWolf");
            if (level > 0)
                _damageable.HealEvenly(body, FixedPoint2.New(-(level * FSPerkBonusConstants.LoneWolfRegenPerLevel)));
        }
    }

    private void Recheck()
    {
        _wasAlone.Clear();
        _wasAlone.UnionWith(_alone);
        _alone.Clear();

        var query = EntityQueryEnumerator<FSPerkLevelsComponent, MindComponent>();
        while (query.MoveNext(out _, out var perks, out var mind))
        {
            if (mind.CurrentEntity is { } body
                && perks.GetSlottedLevel("LoneWolf") > 0
                && !HasTeammateNearby(body))
                _alone.Add(body);
        }

        foreach (var body in _alone)
        {
            if (!_wasAlone.Contains(body))
                _notify.SendStacksToBody(body, "LoneWolf", 1);
        }

        foreach (var body in _wasAlone)
        {
            if (!_alone.Contains(body) && !TerminatingOrDeleted(body))
                _notify.SendStacksToBody(body, "LoneWolf", 0);
        }
    }

    private bool HasTeammateNearby(EntityUid body)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(Transform(body).Coordinates, FSPerkBonusConstants.LoneWolfRadius, _nearby);

        foreach (var other in _nearby)
        {
            if (other.Owner != body
                && other.Comp.HasMind
                && !HasComp<WaveSpawnedTagComponent>(other)
                && _mobState.IsAlive(other))
                return true;
        }

        return false;
    }
}
