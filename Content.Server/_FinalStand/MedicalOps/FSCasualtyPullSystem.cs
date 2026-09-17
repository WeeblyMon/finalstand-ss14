using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

// Surgery is where the Doctor is; this is how the one casualty nobody can reach gets to them.
public sealed class FSCasualtyPullSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly EntProtoId ArriveEffect = "EffectFlashBluespace";

    private static readonly SoundSpecifier ArriveSound =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/dart_hit.ogg");
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private FSMedicalUpgradeSystem _upgrades = default!;

    private static readonly EntProtoId PullActionProto = "FSCasualtyPullAction";

    private static readonly SoundSpecifier PullSound =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/medic_alert.ogg");

    private const string DoctorJob = "MedicalDoctor";

    private readonly Dictionary<EntityUid, EntityUid> _granted = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSCasualtyPullActionEvent>(OnPull);
        SubscribeNetworkEvent<FSCasualtyPullRequestEvent>(OnPullRequest);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawn);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId != DoctorJob)
            return;

        if (_granted.TryGetValue(ev.Mob, out var existing) && existing.IsValid())
            return;

        if (_actions.AddAction(ev.Mob, PullActionProto) is { } action)
            _granted[ev.Mob] = action;

        EnsureComp<FSCasualtyPullComponent>(ev.Mob);
    }

    private void OnPullRequest(FSCasualtyPullRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } doctor
            || !TryComp<FSCasualtyPullComponent>(doctor, out var pull)
            || pull.ReadyAt > _timing.CurTime)
        {
            return;
        }

        TryPull(doctor, GetEntity(ev.Patient), null);
    }

    private void OnPull(FSCasualtyPullActionEvent args)
    {
        TryPull(args.Performer, args.Target, args.Action);
    }

    private void TryPull(EntityUid doctor, EntityUid target, Entity<ActionComponent>? action)
    {
        if (target == doctor || TerminatingOrDeleted(target))
            return;

        // A conscious crewmate is not a casualty, and yanking one is a grief tool.
        if (!_mobState.IsCritical(target) && !_mobState.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("fs-casualty-pull-not-down"), doctor, doctor);
            return;
        }

        if (IsBeingWorkedOn(target))
        {
            _popup.PopupEntity(Loc.GetString("fs-casualty-pull-busy"), doctor, doctor);
            return;
        }

        var doctorCoords = Transform(doctor).Coordinates;

        if (Transform(target).MapID != Transform(doctor).MapID)
        {
            _popup.PopupEntity(Loc.GetString("fs-casualty-pull-unreachable"), doctor, doctor);
            return;
        }

        if (!IsSafeGround(doctorCoords))
        {
            _popup.PopupEntity(Loc.GetString("fs-casualty-pull-no-room"), doctor, doctor);
            return;
        }

        var cooldown = TimeSpan.FromSeconds(_upgrades.Unlocked(FSMedicalUpgradeSystem.RecoveryUplink) ? 80 : 120);

        // Set before the action starts its own cooldown, so Recovery Uplink applies to this use.
        if (action is { } act)
            _actions.SetUseDelay((act.Owner, act.Comp), cooldown);

        if (TryComp<FSCasualtyPullComponent>(doctor, out var pullComp))
        {
            pullComp.ReadyAt = _timing.CurTime + cooldown;
            Dirty(doctor, pullComp);
        }

        var origin = _xform.GetMapCoordinates(target);
        Spawn(ArriveEffect, origin);

        _xform.SetCoordinates(target, doctorCoords);

        Spawn(ArriveEffect, _xform.GetMapCoordinates(target));
        _audio.PlayPvs(PullSound, doctor);
        _audio.PlayPvs(ArriveSound, target);

        _popup.PopupEntity(Loc.GetString("fs-casualty-pull-arrived"), target, target, PopupType.Medium);

    }

    // DoAfters live on the user, so finding one aimed at this casualty means sweeping the runners.
    // Only ever called behind a two-minute cooldown, never per tick.
    private bool IsBeingWorkedOn(EntityUid target)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } user
                || user == target
                || !TryComp<DoAfterComponent>(user, out var doAfters))
            {
                continue;
            }

            foreach (var (_, doAfter) in doAfters.DoAfters)
            {
                if (!doAfter.Cancelled && doAfter.Args.Target == target)
                    return true;
            }
        }

        return false;
    }

    private bool IsSafeGround(EntityCoordinates coords)
    {
        var mapCoords = _xform.ToMapCoordinates(coords);

        return _map.TryFindGridAt(mapCoords, out var gridUid, out var grid)
               && _map.TryGetTileRef(gridUid, grid, coords, out var tileRef)
               && !_turf.IsSpace(tileRef);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _granted.Clear();
    }
}
