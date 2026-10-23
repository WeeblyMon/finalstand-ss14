using Content.Server.Explosion.EntitySystems;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Explosion;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSMartyrSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<ExplosionPrototype> MartyrExplosionType = "FSMartyrExplosion";
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(20);

    private readonly Dictionary<EntityUid, TimeSpan> _readyAt = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _readyAt.Clear());
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical || args.OldMobState == MobState.Critical) return;
        if (HasComp<FSZombieVisualsComponent>(args.Target)) return;
        if (!HasComp<ActorComponent>(args.Target)) return;
        if (!_mind.TryGetMind(args.Target, out var mindId, out _)) return;
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var augs)) return;

        var level = augs.GetSlottedLevel("Martyr");
        if (level <= 0) return;

        var now = _timing.CurTime;
        if (_readyAt.TryGetValue(mindId, out var readyAt) && now < readyAt) return;
        _readyAt[mindId] = now + Cooldown;

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
