using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed class FSDeathAuraSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly FSPerkNotifySystem _notify = default!;

    private static readonly TimeSpan StackDecayTime = TimeSpan.FromSeconds(8);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieKilledByPlayerEvent>(OnZombieKilled);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSDeathAuraComponent>();
        while (query.MoveNext(out var mindId, out var da))
        {
            if (da.Stacks > 0 && now - da.LastKillTime > StackDecayTime)
            {
                da.Stacks = 0;
                _notify.SendStacks(mindId, "DeathAura", 0);
            }
        }
    }

    private void OnZombieKilled(ref FSZombieKilledByPlayerEvent ev)
    {
        var level = ev.Perks.GetSlottedLevel("DeathAura");
        if (level <= 0) return;

        var mindId = ev.MindId;
        var da = EnsureComp<FSDeathAuraComponent>(mindId);
        da.Stacks = Math.Min(level * 5, da.Stacks + 1);
        da.LastKillTime = _timing.CurTime;
        _notify.SendStacks(mindId, "DeathAura", da.Stacks);
    }

}
