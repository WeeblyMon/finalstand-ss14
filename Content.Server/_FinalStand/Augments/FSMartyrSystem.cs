using Content.Server.Explosion.EntitySystems;
using Content.Shared._FinalStand.Augments;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Explosion;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Augments;

public sealed class FSMartyrSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly ProtoId<ExplosionPrototype> MartyrExplosionType = "FSMartyrExplosion";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        // Trigger when the player goes into critical (downed) state.
        if (args.NewMobState != MobState.Critical || args.OldMobState == MobState.Critical) return;
        if (HasComp<FSZombieVisualsComponent>(args.Target)) return;
        if (!HasComp<ActorComponent>(args.Target)) return;
        if (!_mind.TryGetMind(args.Target, out var mindId, out _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var augs)) return;

        var level = augs.GetSlottedLevel("Martyr");
        if (level <= 0) return;

        var coords = _transform.GetMapCoordinates(args.Target);
        _explosion.QueueExplosion(
            coords,
            MartyrExplosionType,
            totalIntensity: level * 120f,
            slope: 6f,
            maxTileIntensity: 14f,
            cause: args.Target,
            tileBreakScale: 0f,
            maxTileBreak: 0,
            canCreateVacuum: false,
            addLog: false);
    }
}
