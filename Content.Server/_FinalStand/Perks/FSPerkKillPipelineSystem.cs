// Resolves "a player killed a wave zombie" once per death and hands the answer to every
// kill-driven perk. Five systems used to subscribe to broadcast MobStateChangedEvent and repeat
// the same five-line prelude, so one death cost five mind lookups and five component fetches.
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;

namespace Content.Server._FinalStand.Perks;

/// <param name="Killer">The killer's body, not the mind.</param>
/// <param name="WasMeleeKill">The killing blow was a melee swing.</param>
[ByRefEvent]
public readonly record struct FSZombieKilledByPlayerEvent(
    EntityUid Zombie,
    EntityUid Killer,
    EntityUid MindId,
    FSPerkLevelsComponent Perks,
    bool WasMeleeKill);

public sealed partial class FSPerkKillPipelineSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;

    // A melee swing applies its damage in the same tick as the hit, so a death this tick is a melee kill.
    private readonly HashSet<(EntityUid User, EntityUid Target)> _meleeHits = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FSPlayerMeleeHitEvent>(OnPlayerMeleeHit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _meleeHits.Clear());
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _meleeHits.Clear();
    }

    private void OnPlayerMeleeHit(FSPlayerMeleeHitEvent ev)
    {
        foreach (var target in ev.Hit.HitEntities)
            _meleeHits.Add((ev.Hit.User, target));
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

        var ev = new FSZombieKilledByPlayerEvent(args.Target, killer, mindId, perks, _meleeHits.Contains((killer, args.Target)));
        RaiseLocalEvent(ref ev);
    }
}
