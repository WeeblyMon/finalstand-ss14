using System.Linq;
using Content.Shared._FinalStand.Ailments;
using Content.Shared._FinalStand.MedicalOps;
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
    [Dependency] private FSMedicalRosterSystem _roster = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private FSAilmentSystem _ailments = default!;

    private static readonly SoundSpecifier RespondSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    private static readonly TimeSpan CallLifetime = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(1);

    private const int MaxPriorityTags = 2;
    private static readonly TimeSpan PriorityDuration = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan PriorityCooldown = TimeSpan.FromSeconds(10);

    private readonly Dictionary<EntityUid, Call> _calls = new();
    private readonly List<EntityUid> _finished = new();
    private readonly Dictionary<EntityUid, TimeSpan> _priority = new();
    private readonly Dictionary<EntityUid, TimeSpan> _tagReadyAt = new();
    private TimeSpan _nextBroadcast;
    private bool _sentEmpty;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<FSCasualtyBoardActionEvent>(OnBoardAction);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FSAilmentInflictedEvent>(OnAilmentInflicted);
        SubscribeNetworkEvent<FSRespondToCasualtyEvent>(OnRespond);
        SubscribeNetworkEvent<FSPriorityTagRequestEvent>(OnPriorityTag);
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (ev.NewMobState is not (MobState.Critical or MobState.Dead))
            return;

        if (!HasComp<ActorComponent>(ev.Target))
            return;

        RegisterCall(ev.Target);
    }

    private void OnAilmentInflicted(ref FSAilmentInflictedEvent ev)
    {
        if (_calls.ContainsKey(ev.Patient) || !HasComp<ActorComponent>(ev.Patient) || !NeedsSilentRow(ev.Patient))
            return;

        _calls[ev.Patient] = new Call
        {
            Expires = _timing.CurTime + CallLifetime,
            Silent = true,
        };

        Broadcast();
    }

    private bool NeedsSilentRow(EntityUid patient)
    {
        return _ailments.IsBroken(patient)
               || _ailments.GetBleedLevel(patient) >= _ailments.Rules.MajorBleedLevel;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _calls.Clear();
        _priority.Clear();
        _tagReadyAt.Clear();
        _nextBroadcast = TimeSpan.Zero;
        _sentEmpty = false;
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
            if (existing.Silent)
            {
                existing.Silent = false;
                existing.CalledHurt = !IsRecovered(patient);
                EnsureComp<FSCasualtyStatusComponent>(patient);
                Broadcast();
            }

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

    public bool IsPriority(EntityUid patient)
    {
        return _priority.TryGetValue(patient, out var until) && until > _timing.CurTime;
    }

    private void OnPriorityTag(FSPriorityTagRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } cmo || !HasComp<FSCmoPanelComponent>(cmo))
            return;

        TogglePriority(cmo, GetEntity(ev.Patient));
    }

    public void TogglePriority(EntityUid cmo, EntityUid patient)
    {
        if (!_calls.ContainsKey(patient))
            return;

        if (_priority.Remove(patient))
        {
            Broadcast();
            return;
        }

        var now = _timing.CurTime;
        if (_tagReadyAt.GetValueOrDefault(cmo) > now)
        {
            _popup.PopupEntity(Loc.GetString("fs-casualty-priority-cooldown"), cmo, cmo);
            return;
        }

        if (_priority.Count >= MaxPriorityTags)
        {
            var oldest = _priority.MinBy(tag => tag.Value).Key;
            _priority.Remove(oldest);
        }

        _priority[patient] = now + PriorityDuration;
        _tagReadyAt[cmo] = now + PriorityCooldown;
        _popup.PopupEntity(Loc.GetString("fs-casualty-priority-tagged", ("patient", Identity.Name(patient, EntityManager))), cmo, cmo);
        Broadcast();
    }

    public void Respond(EntityUid medic, EntityUid patient)
    {
        // Already on the way: the popups, sound and broadcast would only repeat.
        if (!_calls.TryGetValue(patient, out var call) || TerminatingOrDeleted(patient) || call.Responder == medic)
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

    private void SweepCasualties()
    {
        foreach (var session in _player.Sessions)
        {
            // A silent row (bleeding or broken, still standing) is promoted to a real call once they're down.
            if (session.AttachedEntity is not { } mob
                || TerminatingOrDeleted(mob)
                || _calls.TryGetValue(mob, out var call) && !call.Silent)
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

        _finished.Clear();
        foreach (var (patient, call) in _calls)
        {
            // A player who is still down keeps their call (and its responder) however long it takes;
            // the lifetime only ends hurt pings and bodies whose player has left.
            if (TerminatingOrDeleted(patient)
                || call.Expires <= now && !StillDown(patient)
                || (call.CalledHurt && IsRecovered(patient))
                || (call.Silent && _mobState.IsAlive(patient) && !NeedsSilentRow(patient)))
            {
                _finished.Add(patient);
            }
        }

        foreach (var patient in _finished)
        {
            if (!TerminatingOrDeleted(patient))
                RemComp<FSCasualtyStatusComponent>(patient);

            _calls.Remove(patient);
            _priority.Remove(patient);
        }

        foreach (var (patient, until) in _priority)
        {
            if (until <= now)
                _finished.Add(patient);
        }

        foreach (var patient in _finished)
            _priority.Remove(patient);

        if (_finished.Count == 0 && now < _nextBroadcast)
            return;

        // Catches anyone who was already down when they got a body; the state-change hook never saw them.
        SweepCasualties();
        Broadcast();
    }

    private void Broadcast()
    {
        _nextBroadcast = _timing.CurTime + BroadcastInterval;

        // Medics already hold the empty board sent when the last call cleared.
        if (_calls.Count == 0 && _sentEmpty)
            return;

        _sentEmpty = _calls.Count == 0;
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

            var state = StateOf(patient);
            var pips = _ailments.GetPips(patient);
            TryComp<FSAilmentStateComponent>(patient, out var vitals);

            entries.Add(new FSCasualtyEntry
            {
                Patient = GetNetEntity(patient),
                Name = Identity.Name(patient, EntityManager),
                State = state,
                Position = GetNetCoordinates(_xform.GetMoverCoordinates(patient)),
                Responder = responder,
                DownEnd = vitals?.DownEnd,
                Held = vitals?.Held ?? false,
                Pips = pips,
                Needs = NeedsOf(patient, state, pips),
                NeedsDefib = state == FSCasualtyState.Dead,
                Priority = IsPriority(patient),
            });
        }

        return new FSCasualtyBoardEvent(entries, open);
    }

    private FSAilmentPips NeedsOf(EntityUid patient, FSCasualtyState state, FSAilmentPips pips)
    {
        var needs = FSAilmentPips.None;
        var bleed = _ailments.GetBleedLevel(patient);
        if (bleed >= _ailments.Rules.MajorBleedLevel || bleed > 0 && state == FSCasualtyState.Critical)
            needs |= FSAilmentPips.Bleeding;

        if (_ailments.IsBroken(patient))
            needs |= FSAilmentPips.Bone;

        if (_ailments.HasAilment(patient, _ailments.Rules.Poisoned))
            needs |= FSAilmentPips.Bloater;

        return needs | (pips & FSAilmentPips.Burn);
    }

    private FSCasualtyState StateOf(EntityUid patient)
    {
        if (_mobState.IsDead(patient))
            return FSCasualtyState.Dead;

        return _mobState.IsCritical(patient) ? FSCasualtyState.Critical : FSCasualtyState.Hurt;
    }

    private bool StillDown(EntityUid patient)
    {
        return HasComp<ActorComponent>(patient) && (_mobState.IsCritical(patient) || _mobState.IsDead(patient));
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
        public bool Silent;
    }
}
