// Department-wide buffs the CMO calls. Everything lands on every medic, not just the caller.

using Content.Server.Chat.Systems;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSCmoAbilitySystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private FSMedicalBonusSystem _bonus = default!;
    [Dependency] private FSMedicalFundSystem _fund = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IPlayerManager _player = default!;

    public const string McpSource = "mcp";
    public const string DirectiveSource = "directive";
    public const string MobilisationSource = "mobilisation";

    private const string CmoJob = "ChiefMedicalOfficer";

    private static readonly EntProtoId[] CmoActions =
    {
        "FSMassCasualtyProtocolAction",
        "FSMedicalDirectiveTraumaAction",
        "FSMedicalDirectivePharmaAction",
        "FSMedicalDirectiveFieldOpsAction",
        "FSMedicalMobilisationAction",
    };

    private static readonly TimeSpan McpDuration = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan MobilisationDuration = TimeSpan.FromSeconds(20);

    private readonly HashSet<EntityUid> _granted = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        SubscribeLocalEvent<FSMassCasualtyProtocolEvent>(OnMassCasualtyProtocol);
        SubscribeLocalEvent<FSMedicalDirectiveEvent>(OnMedicalDirective);
        SubscribeLocalEvent<FSMedicalMobilisationEvent>(OnMedicalMobilisation);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _granted.Clear();
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId != CmoJob || !_granted.Add(ev.Mob))
            return;

        foreach (var proto in CmoActions)
            _actions.AddAction(ev.Mob, proto);
    }

    private void OnMassCasualtyProtocol(FSMassCasualtyProtocolEvent args)
    {
        args.Handled = true;

        ApplyToDepartment(McpSource, new Dictionary<FSMedicalBonusCategory, float>
        {
            [FSMedicalBonusCategory.TreatmentSpeed] = 0.25f,
            [FSMedicalBonusCategory.RevivalSpeed] = 0.25f,
            [FSMedicalBonusCategory.Stabilisation] = 0.25f,
            [FSMedicalBonusCategory.DefibCooldown] = 0.25f,
        }, McpDuration);

        Announce(args.Performer, "fs-cmo-mcp-announce");
    }

    private void OnMedicalMobilisation(FSMedicalMobilisationEvent args)
    {
        args.Handled = true;

        ApplyToDepartment(MobilisationSource, new Dictionary<FSMedicalBonusCategory, float>
        {
            [FSMedicalBonusCategory.Movement] = 0.30f,
            [FSMedicalBonusCategory.DragSpeed] = 0.60f,
            [FSMedicalBonusCategory.TreatmentSpeed] = 0.25f,
            [FSMedicalBonusCategory.InterruptionResistance] = 0.50f,
        }, MobilisationDuration);

        Announce(args.Performer, "fs-cmo-mobilisation-announce");
    }

    // Directives are persistent and mutually exclusive: they all share one source key, so setting one
    // overwrites whichever was standing.
    private void OnMedicalDirective(FSMedicalDirectiveEvent args)
    {
        args.Handled = true;

        var bonuses = args.Directive switch
        {
            FSMedicalDirective.Trauma => new Dictionary<FSMedicalBonusCategory, float>
            {
                [FSMedicalBonusCategory.RevivalSpeed] = 0.15f,
                [FSMedicalBonusCategory.Stabilisation] = 0.15f,
                [FSMedicalBonusCategory.DefibCooldown] = 0.15f,
            },
            FSMedicalDirective.Pharma => new Dictionary<FSMedicalBonusCategory, float>
            {
                [FSMedicalBonusCategory.TreatmentSpeed] = 0.20f,
            },
            _ => new Dictionary<FSMedicalBonusCategory, float>
            {
                [FSMedicalBonusCategory.Movement] = 0.10f,
                [FSMedicalBonusCategory.DragSpeed] = 0.25f,
                [FSMedicalBonusCategory.InterruptionResistance] = 0.20f,
            },
        };

        ApplyToDepartment(DirectiveSource, bonuses);
        Announce(args.Performer, $"fs-cmo-directive-{args.Directive.ToString().ToLowerInvariant()}-announce");
    }

    private void ApplyToDepartment(string source, Dictionary<FSMedicalBonusCategory, float> bonuses, TimeSpan? duration = null)
    {
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } mob || !_fund.IsMedical(mob))
                continue;

            // Each recipient needs its own dictionary - the buff outlives this call.
            _bonus.ApplyBuff(mob, source, new Dictionary<FSMedicalBonusCategory, float>(bonuses), duration);
        }
    }

    private void Announce(EntityUid performer, string locId)
    {
        var message = Loc.GetString(locId);

        _chat.TrySendInGameICMessage(performer, message, InGameICChatType.Speak, hideChat: false);

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is { } mob && _fund.IsMedical(mob))
                _popup.PopupEntity(message, mob, mob, PopupType.Medium);
        }
    }
}
