using Content.Shared._FinalStand.Perks;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

// Sends each player their active timed perk buffs, only when the set or an end time changes.
public sealed partial class FSPerkTimedBuffNotifySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;

    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(0.25);
    private TimeSpan _nextSweep;
    private readonly Dictionary<EntityUid, List<FSPerkTimedBuff>> _lastSent = new();
    private readonly HashSet<EntityUid> _seen = new();
    private readonly List<EntityUid> _stale = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _lastSent.Clear());
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextSweep)
            return;
        _nextSweep = now + SweepInterval;
        _seen.Clear();

        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var mindId, out var mind))
        {
            if (mind.UserId is not { } userId || !_players.TryGetSessionById(userId, out var session))
                continue;

            _seen.Add(mindId);
            var buffs = Collect(mindId, mind, now);

            if (_lastSent.TryGetValue(mindId, out var last) && SameBuffs(last, buffs))
                continue;

            _lastSent[mindId] = buffs;
            RaiseNetworkEvent(new FSPerkTimedBuffsEvent(buffs), Filter.SinglePlayer(session));
        }

        foreach (var mindId in _lastSent.Keys)
        {
            if (!_seen.Contains(mindId))
                _stale.Add(mindId);
        }

        foreach (var mindId in _stale)
            _lastSent.Remove(mindId);
        _stale.Clear();
    }

    private List<FSPerkTimedBuff> Collect(EntityUid mindId, MindComponent mind, TimeSpan now)
    {
        var buffs = new List<FSPerkTimedBuff>();

        if (TryComp<FSCombatMedicBuffComponent>(mindId, out var medic) && now < medic.EndTime)
            buffs.Add(new FSPerkTimedBuff("CombatMedic",
                $"Combat Medic: +{medic.Level * FSPerkBonusConstants.CombatMedicPerLevel * 100f:0.#}% damage", medic.EndTime));

        if (TryComp<FSBloodloadComponent>(mindId, out var bloodload) && now < bloodload.EndTime)
            buffs.Add(new FSPerkTimedBuff("Bloodload",
                $"Bloodload: +{bloodload.Level * FSPerkBonusConstants.BloodloadPerLevel * 100f:0.#}% reload speed", bloodload.EndTime));

        if (TryComp<FSOfficerBuffComponent>(mindId, out var officer) && now < officer.EndTime)
            buffs.Add(new FSPerkTimedBuff("Officer",
                $"Officer: +{officer.Level * FSPerkBonusConstants.OfficerBuffPerLevel * 100f:0.#}% damage", officer.EndTime));

        if (mind.CurrentEntity is { } body
            && TryComp<FSUndyingComponent>(body, out var undying)
            && undying.EndTime is { } death
            && now < death)
            buffs.Add(new FSPerkTimedBuff("Undying", "Undying: unlimited ammo, then you die", death));

        return buffs;
    }

    private static bool SameBuffs(List<FSPerkTimedBuff> a, List<FSPerkTimedBuff> b)
    {
        if (a.Count != b.Count)
            return false;

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }
}
