// Underdog perk: damage and resistance per living zombie near the player, counted twice a second.
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed class FSUnderdogSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private FSPerkNotifySystem _notify = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan CountInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextCount;
    private readonly HashSet<Entity<WaveSpawnedTagComponent>> _nearby = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSIncomingDamageModifyEvent>(OnIncomingDamage);
    }

    public float GetMultiplier(EntityUid body, FSPerkLevelsComponent perks)
    {
        var level = perks.GetSlottedLevel("Underdog");
        if (level <= 0 || !TryComp<FSUnderdogComponent>(body, out var underdog) || underdog.Count <= 0)
            return 1f;

        return 1f + underdog.Count * FSPerkBonusConstants.UnderdogPerZombie[level - 1];
    }

    private void OnIncomingDamage(ref FSIncomingDamageModifyEvent ev)
    {
        if (ev.Args.Damage.GetTotal() <= 0
            || !_mind.TryGetMind(ev.Target, out var mindId, out _)
            || !TryComp<FSPerkLevelsComponent>(mindId, out var perks))
            return;

        var mult = GetMultiplier(ev.Target, perks);
        if (mult > 1f)
            ev.Args.Damage *= 1f / mult;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCount)
            return;
        _nextCount = now + CountInterval;

        var query = EntityQueryEnumerator<FSPerkLevelsComponent, MindComponent>();
        while (query.MoveNext(out _, out var perks, out var mind))
        {
            if (mind.CurrentEntity is not { } uid)
                continue;

            if (perks.GetSlottedLevel("Underdog") <= 0)
            {
                if (RemComp<FSUnderdogComponent>(uid))
                    _notify.SendStacksToBody(uid, "Underdog", 0);
                continue;
            }

            _nearby.Clear();
            _lookup.GetEntitiesInRange(Transform(uid).Coordinates, FSPerkBonusConstants.UnderdogRadius, _nearby);

            var count = 0;
            foreach (var zombie in _nearby)
            {
                if (_mobState.IsAlive(zombie))
                    count++;
            }
            count = Math.Min(count, FSPerkBonusConstants.UnderdogMaxZombies);

            var underdog = EnsureComp<FSUnderdogComponent>(uid);
            if (underdog.Count == count)
                continue;

            underdog.Count = count;
            _notify.SendStacksToBody(uid, "Underdog", count);
        }
    }
}
