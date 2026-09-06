// Tracks who has called for a medic and who is on the way.

using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Actions;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSCasualtySystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private FSMedicalFundSystem _fund = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly EntProtoId BoardAction = "FSCasualtyBoardAction";
    private static readonly TimeSpan CallLifetime = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(1);

    private readonly Dictionary<EntityUid, Call> _calls = new();
    private readonly List<EntityUid> _finished = new();
    private TimeSpan _nextBroadcast;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<FSCasualtyBoardActionEvent>(OnBoardAction);
        SubscribeNetworkEvent<FSRespondToCasualtyEvent>(OnRespond);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _calls.Clear();
        _nextBroadcast = TimeSpan.Zero;
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (_fund.IsMedical(ev.Mob))
            _actions.AddAction(ev.Mob, BoardAction);
    }

    private void OnBoardAction(FSCasualtyBoardActionEvent args)
    {
        args.Handled = true;

        if (_player.TryGetSessionByEntity(args.Performer, out var session))
            RaiseNetworkEvent(BuildBoard(open: true), session);
    }

    public void RegisterCall(EntityUid patient)
    {
        if (_calls.TryGetValue(patient, out var existing))
        {
            existing.Expires = _timing.CurTime + CallLifetime;
            return;
        }

        _calls[patient] = new Call
        {
            Expires = _timing.CurTime + CallLifetime,
            CalledHurt = !IsRecovered(patient),
        };

        Broadcast();
    }

    private void OnRespond(FSRespondToCasualtyEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } medic || !_fund.IsMedical(medic))
            return;

        var patient = GetEntity(ev.Patient);
        if (!_calls.TryGetValue(patient, out var call) || TerminatingOrDeleted(patient))
            return;

        call.Responder = medic;

        var medicName = Identity.Name(medic, EntityManager);
        _popup.PopupEntity(Loc.GetString("fs-casualty-en-route", ("medic", medicName)), patient, patient, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("fs-casualty-responding", ("patient", Identity.Name(patient, EntityManager))), medic, medic);

        Broadcast();
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        _finished.Clear();
        foreach (var (patient, call) in _calls)
        {
            // Recovery only clears a call from someone who was actually hurt when they made it -
            // otherwise a deliberate call from a healthy player was pruned on the very next tick.
            if (TerminatingOrDeleted(patient)
                || call.Expires <= now
                || (call.CalledHurt && IsRecovered(patient)))
            {
                _finished.Add(patient);
            }
        }

        foreach (var patient in _finished)
            _calls.Remove(patient);

        if (_finished.Count > 0)
        {
            Broadcast();
            return;
        }

        if (now < _nextBroadcast || _calls.Count == 0)
            return;

        Broadcast();
    }

    // Positions and conditions move, so the board is refreshed on a timer rather than only on change.
    private void Broadcast()
    {
        _nextBroadcast = _timing.CurTime + BroadcastInterval;

        var board = BuildBoard();
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is { } mob && _fund.IsMedical(mob))
                RaiseNetworkEvent(board, session);
        }
    }

    private FSCasualtyBoardEvent BuildBoard(bool open = false)
    {
        var entries = new List<FSCasualtyEntry>(_calls.Count);

        foreach (var (patient, call) in _calls)
        {
            if (TerminatingOrDeleted(patient))
                continue;

            string? responder = null;
            if (call.Responder is { } medic && !TerminatingOrDeleted(medic))
                responder = Identity.Name(medic, EntityManager);

            entries.Add(new FSCasualtyEntry
            {
                Patient = GetNetEntity(patient),
                Name = Identity.Name(patient, EntityManager),
                State = StateOf(patient),
                Position = GetNetCoordinates(Transform(patient).Coordinates),
                Responder = responder,
            });
        }

        return new FSCasualtyBoardEvent(entries, open);
    }

    private FSCasualtyState StateOf(EntityUid patient)
    {
        if (_mobState.IsDead(patient))
            return FSCasualtyState.Dead;

        return _mobState.IsCritical(patient) ? FSCasualtyState.Critical : FSCasualtyState.Hurt;
    }

    private bool IsRecovered(EntityUid patient)
    {
        return _mobState.IsAlive(patient)
               && TryComp<DamageableComponent>(patient, out var damageable)
               && _damageable.GetTotalDamage((patient, (DamageableComponent?)damageable)) <= 0;
    }

    private sealed class Call
    {
        public TimeSpan Expires;
        public EntityUid? Responder;
        public bool CalledHurt;
    }
}
