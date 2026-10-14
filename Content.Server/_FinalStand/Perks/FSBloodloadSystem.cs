using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSBloodloadSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private FSPerkNotifySystem _notify = default!;

    private static readonly TimeSpan BuffDuration = TimeSpan.FromSeconds(FSPerkBonusConstants.BloodloadSeconds);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieKilledByPlayerEvent>(OnZombieKilled);
    }

    /// <summary>Reload time multiplier for the user; below 1 is faster.</summary>
    public float GetReloadTimeMultiplier(EntityUid user)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _)
            || !TryComp<FSBloodloadComponent>(mindId, out var buff)
            || _timing.CurTime >= buff.EndTime)
            return 1f;

        return 1f / (1f + buff.Level * FSPerkBonusConstants.BloodloadPerLevel);
    }

    private void OnZombieKilled(ref FSZombieKilledByPlayerEvent ev)
    {
        if (!ev.WasMeleeKill)
            return;

        var level = ev.Perks.GetSlottedLevel("Bloodload");
        if (level <= 0)
            return;

        var buff = EnsureComp<FSBloodloadComponent>(ev.MindId);
        buff.EndTime = _timing.CurTime + BuffDuration;
        buff.Level = level;
        _notify.SendStacks(ev.MindId, "Bloodload", 1);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSBloodloadComponent>();
        while (query.MoveNext(out var uid, out var buff))
        {
            if (now < buff.EndTime)
                continue;

            RemCompDeferred<FSBloodloadComponent>(uid);
            _notify.SendStacks(uid, "Bloodload", 0);
        }
    }
}
