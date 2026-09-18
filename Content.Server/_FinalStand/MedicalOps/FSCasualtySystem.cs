using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Actions;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSCasualtySystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private FSMedicalRosterSystem _roster = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier RespondSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    private const float MedicalSurgerySpeed = 4f;
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
        // Broadcast, not directed. SharedStunSystem already owns the directed
        // (MobStateComponent, MobStateChangedEvent) pair, and that pair is a global namespace - a
        // second subscriber is a startup throw, not a silent override. Broadcast is a separate
        // namespace, so any number of systems can take this event that way.
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeNetworkEvent<FSRespondToCasualtyEvent>(OnRespond);
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (ev.NewMobState is not (MobState.Critical or MobState.Dead))
            return;

        if (!HasComp<ActorComponent>(ev.Target))
            return;

        RegisterCall(ev.Target);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _calls.Clear();
        _nextBroadcast = TimeSpan.Zero;
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (!_roster.IsMedicalJob(ev.JobId))
            return;

        // A 19 second rooted do-after only fits between waves. Medical staff operate fast enough
        // that surgery is something you can choose to do while the round is happening.
        EnsureComp<SurgerySpeedModifierComponent>(ev.Mob).SpeedModifier = MedicalSurgerySpeed;

        // Otherwise every operation needs the patient stripped first, which is not a thing anyone
        // is doing while a wave is on.
        EnsureComp<SurgeryIgnoreClothingComponent>(ev.Mob);
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

        EnsureComp<FSCasualtyStatusComponent>(patient);

        Broadcast();
    }

    private void OnRespond(FSRespondToCasualtyEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } medic || !_roster.IsMedical(medic))
            return;

        Respond(medic, GetEntity(ev.Patient));
    }

    public void Respond(EntityUid medic, EntityUid patient)
    {
        if (!_calls.TryGetValue(patient, out var call) || TerminatingOrDeleted(patient))
            return;

        call.Responder = medic;

        var medicName = Identity.Name(medic, EntityManager);

        var status = EnsureComp<FSCasualtyStatusComponent>(patient);
        status.Responder = medicName;
        Dirty(patient, status);

        _popup.PopupEntity(Loc.GetString("fs-casualty-en-route", ("medic", medicName)), patient, patient, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("fs-casualty-responding", ("patient", Identity.Name(patient, EntityManager))), medic, medic);

        _audio.PlayEntity(RespondSound, patient, patient);

        Broadcast();
    }

    // The state-change event is an edge: miss it once - because the mob had no session attached at
    // that instant, or another handler threw first - and that casualty is off the board for the rest
    // of the round. This sweep reconciles from what the mobs actually are, so it cannot miss.
    private void SweepCasualties()
    {
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } mob
                || TerminatingOrDeleted(mob)
                || _calls.ContainsKey(mob))
            {
                continue;
            }

            if (_mobState.IsCritical(mob) || _mobState.IsDead(mob))
                RegisterCall(mob);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        SweepCasualties();

        _finished.Clear();
        foreach (var (patient, call) in _calls)
        {
            if (TerminatingOrDeleted(patient)
                || call.Expires <= now
                || (call.CalledHurt && IsRecovered(patient)))
            {
                _finished.Add(patient);
            }
        }

        foreach (var patient in _finished)
        {
            if (!TerminatingOrDeleted(patient))
                RemComp<FSCasualtyStatusComponent>(patient);

            _calls.Remove(patient);
        }

        if (_finished.Count > 0)
        {
            Broadcast();
            return;
        }

        if (now >= _nextBroadcast)
            Broadcast();
    }

    private void Broadcast()
    {
        _nextBroadcast = _timing.CurTime + BroadcastInterval;

        var board = BuildBoard();
        foreach (var (session, _) in _roster.Medics())
            RaiseNetworkEvent(board, session);
    }

    public FSCasualtyBoardEvent BuildBoard(bool open = false)
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
