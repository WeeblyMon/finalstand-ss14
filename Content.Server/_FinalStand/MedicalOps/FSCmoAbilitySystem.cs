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

    private const string McpSource = "mcp";
    private const string DirectiveSource = "directive";
    private const string MobilisationSource = "mobilisation";

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

    private static readonly Dictionary<FSMedicalDirective, DirectiveDef> Directives = new()
    {
        [FSMedicalDirective.Trauma] = new DirectiveDef("fs-cmo-directive-trauma-announce",
            new Dictionary<FSMedicalBonusCategory, float>
            {
                [FSMedicalBonusCategory.RevivalSpeed] = 0.15f,
                [FSMedicalBonusCategory.Stabilisation] = 0.15f,
                [FSMedicalBonusCategory.DefibCooldown] = 0.15f,
            }),
        [FSMedicalDirective.Pharma] = new DirectiveDef("fs-cmo-directive-pharma-announce",
            new Dictionary<FSMedicalBonusCategory, float>
            {
                [FSMedicalBonusCategory.TreatmentSpeed] = 0.20f,
            }),
        [FSMedicalDirective.FieldOps] = new DirectiveDef("fs-cmo-directive-fieldops-announce",
            new Dictionary<FSMedicalBonusCategory, float>
            {
                [FSMedicalBonusCategory.Movement] = 0.10f,
                [FSMedicalBonusCategory.DragSpeed] = 0.25f,
                [FSMedicalBonusCategory.InterruptionResistance] = 0.20f,
            }),
    };

    private static readonly Dictionary<FSMedicalBonusCategory, float> McpBonuses = new()
    {
        [FSMedicalBonusCategory.TreatmentSpeed] = 0.25f,
        [FSMedicalBonusCategory.RevivalSpeed] = 0.25f,
        [FSMedicalBonusCategory.Stabilisation] = 0.25f,
        [FSMedicalBonusCategory.DefibCooldown] = 0.25f,
    };

    private static readonly Dictionary<FSMedicalBonusCategory, float> MobilisationBonuses = new()
    {
        [FSMedicalBonusCategory.Movement] = 0.30f,
        [FSMedicalBonusCategory.DragSpeed] = 0.60f,
        [FSMedicalBonusCategory.TreatmentSpeed] = 0.25f,
        [FSMedicalBonusCategory.InterruptionResistance] = 0.50f,
    };

    private FSMedicalDirective? _activeDirective;

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
        _activeDirective = null;
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == CmoJob)
        {
            foreach (var proto in CmoActions)
                _actions.AddAction(ev.Mob, proto);
        }

        // A directive is a standing order, so anyone arriving after it was issued still gets it.
        if (_activeDirective is { } active
            && Directives.TryGetValue(active, out var def)
            && _fund.IsMedical(ev.Mob))
        {
            _bonus.ApplyBuff(ev.Mob, DirectiveSource, def.Bonuses);
        }
    }

    private void OnMassCasualtyProtocol(FSMassCasualtyProtocolEvent args)
    {
        args.Handled = true;

        ApplyToDepartment(McpSource, McpBonuses, McpDuration);
        Announce(args.Performer, "fs-cmo-mcp-announce");
    }

    private void OnMedicalMobilisation(FSMedicalMobilisationEvent args)
    {
        args.Handled = true;

        ApplyToDepartment(MobilisationSource, MobilisationBonuses, MobilisationDuration);
        Announce(args.Performer, "fs-cmo-mobilisation-announce");
    }

    private void OnMedicalDirective(FSMedicalDirectiveEvent args)
    {
        args.Handled = true;

        if (!Directives.TryGetValue(args.Directive, out var def))
            return;

        // Pressing the standing directive again stands it down.
        if (_activeDirective == args.Directive)
        {
            _activeDirective = null;
            RemoveFromDepartment(DirectiveSource);
            Announce(args.Performer, "fs-cmo-directive-stand-down");
            return;
        }

        // All three share one source key, so issuing one replaces whichever was standing.
        _activeDirective = args.Directive;
        ApplyToDepartment(DirectiveSource, def.Bonuses);
        Announce(args.Performer, def.Announcement);
    }

    private void ApplyToDepartment(string source, Dictionary<FSMedicalBonusCategory, float> bonuses, TimeSpan? duration = null)
    {
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is { } mob && _fund.IsMedical(mob))
                _bonus.ApplyBuff(mob, source, bonuses, duration);
        }
    }

    private void RemoveFromDepartment(string source)
    {
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is { } mob)
                _bonus.RemoveBuff(mob, source);
        }
    }

    private void Announce(EntityUid performer, string locId)
    {
        var message = Loc.GetString(locId);

        _chat.TrySendInGameICMessage(performer, message, InGameICChatType.Speak, hideChat: false);

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is { } mob && mob != performer && _fund.IsMedical(mob))
                _popup.PopupEntity(message, mob, mob, PopupType.Medium);
        }
    }

    private readonly record struct DirectiveDef(string Announcement, Dictionary<FSMedicalBonusCategory, float> Bonuses);
}
