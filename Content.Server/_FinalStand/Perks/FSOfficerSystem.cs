using Content.Server._FinalStand.Leveling;
using Content.Server.Popups;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Actions;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

// Owns FSWhistleActionEvent — officer whistle action buffs nearby allies.
public sealed class FSOfficerSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly FSPlayerBonusSummarySystem _bonusSummary = default!;

    private const float WhistleRange = 10f;
    private static readonly TimeSpan BuffDuration = TimeSpan.FromSeconds(8);
    private static readonly EntProtoId WhistleActionProto = "FSWhistleAction";
    private static readonly SoundSpecifier WhistleSound = new SoundCollectionSpecifier("TrenchWhistle");

    // mob entity → granted action entity
    private readonly Dictionary<EntityUid, EntityUid> _grantedActions = new();

    private TimeSpan _nextActionSync;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSWhistleActionEvent>(OnWhistleAction);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var buffQuery = EntityQueryEnumerator<FSOfficerBuffComponent>();
        while (buffQuery.MoveNext(out var uid, out var buff))
        {
            if (now >= buff.EndTime)
                RemComp<FSOfficerBuffComponent>(uid);
        }

        // Sync whistle action every 3s to catch mid-round slot changes.
        if (now < _nextActionSync) return;
        _nextActionSync = now + TimeSpan.FromSeconds(3);

        var augQuery = EntityQueryEnumerator<FSPerkLevelsComponent>();
        while (augQuery.MoveNext(out var mindId, out var augs))
        {
            if (!TryComp<MindComponent>(mindId, out var mind) || !mind.CurrentEntity.HasValue) continue;
            var mob = mind.CurrentEntity.Value;
            if (!TryComp<ActorComponent>(mob, out _)) continue;

            var hasOfficer = augs.GetSlottedLevel("Officer") > 0;
            var hasAction = _grantedActions.TryGetValue(mob, out var existing) && existing.IsValid();

            if (hasOfficer && !hasAction)
            {
                var actionEnt = _actions.AddAction(mob, WhistleActionProto);
                if (actionEnt != null)
                    _grantedActions[mob] = actionEnt.Value;
            }
            else if (!hasOfficer && hasAction)
            {
                _actions.RemoveAction(mob, existing);
                _grantedActions.Remove(mob);
            }
        }
    }

    private void OnWhistleAction(FSWhistleActionEvent args)
    {
        args.Handled = true;

        var user = args.Performer;
        if (!_mind.TryGetMind(user, out var mindId, out MindComponent? _)) return;
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var augs)) return;
        var level = augs.GetSlottedLevel("Officer");
        if (level <= 0) return;

        _audio.PlayPvs(WhistleSound, user);

        var pos = _transform.GetMapCoordinates(user);
        var targets = new HashSet<Entity<ActorComponent>>();
        _lookup.GetEntitiesInRange(pos, WhistleRange, targets);

        foreach (var (targetUid, _) in targets)
        {
            if (targetUid == user) continue;
            if (!_mind.TryGetMind(targetUid, out var targetMind, out MindComponent? _)) continue;
            var buff = EnsureComp<FSOfficerBuffComponent>(targetMind);
            buff.EndTime = _timing.CurTime + BuffDuration;
            buff.Level = level;

            _popup.PopupEntity("Buffed!", targetUid, Filter.Pvs(targetUid), true, PopupType.Medium);

            if (TryComp<ActorComponent>(targetUid, out var actor))
                _bonusSummary.RecomputeFor(targetUid, actor.PlayerSession);
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent _)
    {
        _grantedActions.Clear();
    }
}
