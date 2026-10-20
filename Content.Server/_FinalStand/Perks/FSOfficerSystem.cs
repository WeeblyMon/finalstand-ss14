using Content.Server._FinalStand.Actions;
using Content.Server._FinalStand.WaveHud;
using Content.Server._FinalStand.Leveling;
using Content.Server.Popups;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSOfficerSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FSGrantedActionsSystem _grants = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private FSPlayerBonusSummarySystem _bonusSummary = default!;

    private const float WhistleRange = 10f;
    private static readonly TimeSpan BuffDuration = TimeSpan.FromSeconds(8);
    private static readonly EntProtoId WhistleActionProto = "FSWhistleAction";
    private static readonly SoundSpecifier WhistleSound = new SoundCollectionSpecifier("TrenchWhistle");

    private TimeSpan _nextActionSync;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSWhistleActionEvent>(OnWhistleAction);
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
            // A ghost is the mind's current entity too, and mustn't get a whistle that buffs the living.
            if (!HasComp<ActorComponent>(mob) || !HasComp<MobStateComponent>(mob)) continue;

            if (augs.GetSlottedLevel("Officer") > 0)
                _grants.Grant(mob, WhistleActionProto);
            else
                _grants.Revoke(mob, WhistleActionProto);
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

            _popup.PopupEntity(Loc.GetString("fs-officer-buffed"), targetUid, Filter.Pvs(targetUid), true, PopupType.Medium);

            if (TryComp<ActorComponent>(targetUid, out var actor))
                _bonusSummary.RecomputeFor(targetUid, actor.PlayerSession);
        }
    }
}
