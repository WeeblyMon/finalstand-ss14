// Resolves "a player killed a wave zombie" once per death and hands the answer to every
// kill-driven perk. Five systems used to subscribe to broadcast MobStateChangedEvent and repeat
// the same five-line prelude, so one death cost five mind lookups and five component fetches.
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Mind;
using Content.Shared.Mobs;

namespace Content.Server._FinalStand.Perks;

/// <param name="Killer">The killer's body, not the mind.</param>
[ByRefEvent]
public readonly record struct FSZombieKilledByPlayerEvent(
    EntityUid Zombie,
    EntityUid Killer,
    EntityUid MindId,
    FSPerkLevelsComponent Perks);

public sealed partial class FSPerkKillPipelineSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;
        if (!HasComp<FSZombieVisualsComponent>(args.Target))
            return;
        if (args.Origin is not { } killer)
            return;
        if (!_mind.TryGetMind(killer, out var mindId, out _))
            return;
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var perks))
            return;

        var ev = new FSZombieKilledByPlayerEvent(args.Target, killer, mindId, perks);
        RaiseLocalEvent(ref ev);
    }
}
