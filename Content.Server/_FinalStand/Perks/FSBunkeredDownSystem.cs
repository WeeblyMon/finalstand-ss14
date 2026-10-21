using Content.Server._FinalStand.CCC;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSBunkeredDownSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private FSPerkNotifySystem _notify = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextCheck;
    private readonly HashSet<EntityUid> _bunkered = [];
    private readonly HashSet<EntityUid> _wasBunkered = [];

    public bool IsBunkered(EntityUid mindId) => _bunkered.Contains(mindId);

    public float GetDamageMultiplier(EntityUid mindId, FSPerkLevelsComponent perks)
    {
        var level = perks.GetSlottedLevel("BunkeredDown");
        if (level <= 0)
            return 1f;

        return _bunkered.Contains(mindId)
            ? 1f + level * FSPerkBonusConstants.BunkeredDownDamagePerLevel
            : 1f - FSPerkBonusConstants.BunkeredDownAwayPenalty;
    }

    public float GetResist(EntityUid mindId, FSPerkLevelsComponent perks)
    {
        var level = perks.GetSlottedLevel("BunkeredDown");
        return level > 0 && _bunkered.Contains(mindId) ? level * FSPerkBonusConstants.BunkeredDownResistPerLevel : 0f;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;
        _nextCheck = now + CheckInterval;

        _wasBunkered.Clear();
        _wasBunkered.UnionWith(_bunkered);
        _bunkered.Clear();

        var query = EntityQueryEnumerator<FSPerkLevelsComponent, MindComponent>();
        while (query.MoveNext(out var mindId, out var perks, out var mind))
        {
            if (mind.CurrentEntity is { } body
                && perks.GetSlottedLevel("BunkeredDown") > 0
                && NearCcc(body))
                _bunkered.Add(mindId);
        }

        foreach (var mindId in _bunkered)
        {
            if (!_wasBunkered.Contains(mindId))
                Notify(mindId, 1);
        }

        foreach (var mindId in _wasBunkered)
        {
            if (!_bunkered.Contains(mindId))
                Notify(mindId, 0);
        }
    }

    private void Notify(EntityUid mindId, int stacks)
    {
        if (TryComp<MindComponent>(mindId, out var mind) && mind.CurrentEntity is { } body)
            _notify.SendStacksToBody(body, "BunkeredDown", stacks);
    }

    private bool NearCcc(EntityUid body)
    {
        var position = _transform.GetMapCoordinates(body);
        var query = EntityQueryEnumerator<FinalStandCCCComponent, TransformComponent>();
        while (query.MoveNext(out var ccc, out _, out var xform))
        {
            var cccPosition = _transform.GetMapCoordinates(ccc, xform);
            if (cccPosition.MapId == position.MapId
                && (cccPosition.Position - position.Position).Length() <= FSPerkBonusConstants.BunkeredDownRadius)
                return true;
        }

        return false;
    }
}
