using Content.Server._FinalStand.Augments;
using Content.Shared._FinalStand.Augments;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.Damage.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Augments;

public sealed class FSAdrenalineSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly float[] Durations = [2.1f, 2.8f, 3.5f, 4.2f];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSAdrenalineComponent, StaminaComponent>();
        while (query.MoveNext(out var uid, out var adr, out var stamina))
        {
            if (now >= adr.EndTime)
            {
                RemComp<FSAdrenalineComponent>(uid);
                continue;
            }
            _stamina.TakeStaminaDamage(uid, -(stamina.CritThreshold * frameTime * 10f));
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead) return;
        if (!HasComp<FSZombieVisualsComponent>(args.Target)) return;
        if (!args.Origin.HasValue) return;
        if (!_mind.TryGetMind(args.Origin.Value, out var mindId, out _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var augs)) return;

        var level = augs.GetSlottedLevel("Adrenaline");
        if (level <= 0) return;

        var adr = EnsureComp<FSAdrenalineComponent>(args.Origin.Value);
        adr.EndTime = _timing.CurTime + TimeSpan.FromSeconds(Durations[level - 1]);
    }
}
