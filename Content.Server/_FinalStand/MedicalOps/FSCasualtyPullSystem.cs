using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Actions;
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

namespace Content.Server._FinalStand.MedicalOps;

// Surgery is where the Doctor is; this is how the one casualty nobody can reach gets to them.
public sealed class FSCasualtyPullSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private IPlayerManager _players = default!;

    private static readonly EntProtoId PullActionProto = "FSCasualtyPullAction";

    private static readonly SoundSpecifier PullSound =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/medic_alert.ogg");

    private const string DoctorJob = "MedicalDoctor";

    private readonly Dictionary<EntityUid, EntityUid> _granted = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSCasualtyPullActionEvent>(OnPull);
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
    }

    private void OnPull(FSCasualtyPullActionEvent args)
    {
        var doctor = args.Performer;
        var target = args.Target;

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

        _xform.SetCoordinates(target, doctorCoords);
        _audio.PlayPvs(PullSound, doctor);

        _popup.PopupEntity(Loc.GetString("fs-casualty-pull-arrived"), target, target, PopupType.Medium);

        args.Handled = true;
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
