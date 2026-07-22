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

    private static readonly TimeSpan StackDecayTime = TimeSpan.FromSeconds(8);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
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
                SendStacksUpdate(mindId, "DeathAura", 0);
            }
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead) return;
        if (!HasComp<FSZombieVisualsComponent>(args.Target)) return;
        if (!args.Origin.HasValue) return;
        if (!_mind.TryGetMind(args.Origin.Value, out var mindId, out _)) return;
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var augs)) return;

        var level = augs.GetSlottedLevel("DeathAura");
        if (level <= 0) return;

        var da = EnsureComp<FSDeathAuraComponent>(mindId);
        da.Stacks = Math.Min(level * 5, da.Stacks + 1);
        da.LastKillTime = _timing.CurTime;
        SendStacksUpdate(mindId, "DeathAura", da.Stacks);
    }

    private void SendStacksUpdate(EntityUid mindId, string PerkId, int stacks)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || !mind.CurrentEntity.HasValue) return;
        if (!TryComp<ActorComponent>(mind.CurrentEntity.Value, out var actor)) return;
        RaiseNetworkEvent(new FSPerkStacksUpdateEvent { PerkId = PerkId, Stacks = stacks },
            Filter.SinglePlayer(actor.PlayerSession));
    }
}
