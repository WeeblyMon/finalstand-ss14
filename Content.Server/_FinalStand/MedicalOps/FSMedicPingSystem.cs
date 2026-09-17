using Content.Server.Chat.Systems;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Actions;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSMedicPingSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private FSCasualtySystem _casualty = default!;
    [Dependency] private FSMedicalRosterSystem _roster = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IPlayerManager _players = default!;

    private static readonly EntProtoId PingActionProto = "FSMedicPingAction";
    private static readonly EntProtoId ChemRequestActionProto = "FSChemRequestAction";
    private const string ScreamEmote = "Scream";

    private static readonly SoundSpecifier MedicAlertSound =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/medic_alert.ogg");

    private static readonly SoundSpecifier ChemRequestSound =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/chem_request.ogg");

    private const float HurtThreshold = 0.3f;

    private readonly Dictionary<EntityUid, EntityUid> _grantedActions = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMedicPingActionEvent>(OnPingAction);
        SubscribeLocalEvent<FSChemRequestActionEvent>(OnChemRequestAction);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnChemRequestAction(FSChemRequestActionEvent args)
    {
        args.Handled = true;

        RaiseNetworkEvent(
            new FSMedicPingEvent(GetNetEntity(args.Performer), false, FSPingKind.Chem),
            Filter.Broadcast());

        _audio.PlayEntity(ChemRequestSound, _roster.MedicalFilter(), args.Performer, true);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        var mob = ev.Entity;
        if (_grantedActions.TryGetValue(mob, out var existing) && existing.IsValid())
            return;

        var actionEnt = _actions.AddAction(mob, PingActionProto);
        if (actionEnt != null)
            _grantedActions[mob] = actionEnt.Value;

        _actions.AddAction(mob, ChemRequestActionProto);
    }

    private void OnPingAction(FSMedicPingActionEvent args)
    {
        args.Handled = true;

        var user = args.Performer;

        _chat.TryEmoteWithChat(user, ScreamEmote, ignoreActionBlocker: true, forceEmote: true);
        RaiseNetworkEvent(new FSMedicPingEvent(GetNetEntity(user), IsHurt(user)), Filter.Broadcast());
        _casualty.RegisterCall(user);

        _audio.PlayEntity(MedicAlertSound, _roster.MedicalFilter(), user, true);
    }

    private bool IsHurt(EntityUid uid)
    {
        if (_mobState.IsIncapacitated(uid))
            return true;

        if (!TryComp<DamageableComponent>(uid, out var damageable)
            || !_thresholds.TryGetThresholdForState(uid, MobState.Critical, out var threshold)
            || threshold is not { } critThreshold
            || critThreshold <= 0)
            return false;

        var total = (float)_damageable.GetTotalDamage((uid, (DamageableComponent?)damageable));
        return total / critThreshold.Float() >= HurtThreshold;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _grantedActions.Clear();
    }
}
