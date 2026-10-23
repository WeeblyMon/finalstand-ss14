using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSWolfPackSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private FSLoneWolfSystem _loneWolf = default!;
    [Dependency] private FSPerkNotifySystem _notify = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextCheck;
    private readonly HashSet<EntityUid> _packed = [];
    private readonly HashSet<EntityUid> _wasPacked = [];

    public float GetResist(EntityUid mindId, FSPerkLevelsComponent perks)
    {
        var level = perks.GetSlottedLevel("WolfPack");
        return level > 0 && _packed.Contains(mindId) ? level * FSPerkBonusConstants.WolfPackResistPerLevel : 0f;
    }

    public float GetReloadTimeMultiplier(EntityUid user)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _)
            || !TryComp<FSPerkLevelsComponent>(mindId, out var perks))
            return 1f;

        var level = perks.GetSlottedLevel("WolfPack");
        return level > 0 && _packed.Contains(mindId) ? 1f / (1f + level * FSPerkBonusConstants.WolfPackReloadPerLevel) : 1f;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;
        _nextCheck = now + CheckInterval;

        _wasPacked.Clear();
        _wasPacked.UnionWith(_packed);
        _packed.Clear();

        var query = EntityQueryEnumerator<FSPerkLevelsComponent, MindComponent>();
        while (query.MoveNext(out var mindId, out var perks, out var mind))
        {
            if (mind.CurrentEntity is { } body
                && perks.GetSlottedLevel("WolfPack") > 0
                && _loneWolf.HasTeammateWithin(body, FSPerkBonusConstants.WolfPackRadius))
                _packed.Add(mindId);
        }

        foreach (var mindId in _packed)
        {
            if (!_wasPacked.Contains(mindId))
                Notify(mindId, 1);
        }

        foreach (var mindId in _wasPacked)
        {
            if (!_packed.Contains(mindId))
                Notify(mindId, 0);
        }
    }

    private void Notify(EntityUid mindId, int stacks)
    {
        if (TryComp<MindComponent>(mindId, out var mind) && mind.CurrentEntity is { } body)
            _notify.SendStacksToBody(body, "WolfPack", stacks);
    }
}
