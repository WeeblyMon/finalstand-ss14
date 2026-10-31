using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Client._FinalStand.Mobs;

// Crit and dead thresholds per mob for overlays that draw every frame. MobThresholdSystem finds them by
// walking a SortedDictionary, and that enumerator allocates.
public sealed partial class FSMobThresholdCacheSystem : EntitySystem
{
    private readonly Dictionary<EntityUid, Cached> _cache = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobThresholdsComponent, ComponentRemove>(OnRemove);
    }

    private void OnRemove(Entity<MobThresholdsComponent> ent, ref ComponentRemove args)
    {
        _cache.Remove(ent);
    }

    public void GetIncapThresholds(Entity<MobThresholdsComponent> ent, out FixedPoint2? crit, out FixedPoint2? dead)
    {
        var thresholds = ent.Comp.Thresholds;

        // Handling a server state swaps in a new dictionary, so a different instance means the entry is stale.
        if (!_cache.TryGetValue(ent, out var cached) || !ReferenceEquals(cached.Source, thresholds))
        {
            FixedPoint2? critFound = null;
            FixedPoint2? deadFound = null;

            // Sorted by damage, so the first match is what TryGetThresholdForState would return.
            foreach (var (damage, state) in thresholds)
            {
                if (state == MobState.Critical)
                    critFound ??= damage;
                else if (state == MobState.Dead)
                    deadFound ??= damage;
            }

            cached = new Cached(thresholds, critFound, deadFound);
            _cache[ent] = cached;
        }

        crit = cached.Crit;
        dead = cached.Dead;
    }

    private readonly record struct Cached(
        SortedDictionary<FixedPoint2, MobState> Source,
        FixedPoint2? Crit,
        FixedPoint2? Dead);
}
