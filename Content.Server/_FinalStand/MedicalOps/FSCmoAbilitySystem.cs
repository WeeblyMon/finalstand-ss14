using System.Collections.Frozen;
using Content.Server.Administration.Logs;
using Content.Server._FinalStand.Economy;
using Content.Shared.Mind;
using Content.Server._FinalStand.Research;
using Content.Shared.Database;
using Content.Server.Radio.EntitySystems;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.GameTicking;
using Content.Shared.Radio;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSCmoAbilitySystem : EntitySystem
{
    [Dependency] private FSMedicalBonusSystem _bonus = default!;
    [Dependency] private FSMedicalRosterSystem _roster = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private FSMedicalUpgradeSystem _upgrades = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private FSMedicalFundSystem _fund = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    private static readonly SoundSpecifier DirectiveSound =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/directive.ogg");

    private static readonly SoundSpecifier StandDownSound =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/directive_standdown.ogg");

    private static readonly ProtoId<RadioChannelPrototype> MedicalChannel = "Medical";

    private const string McpSource = "mcp";
    private const string DirectiveSource = "directive";
    private const string MobilisationSource = "mobilisation";
    private const string DoctrineSource = "doctrine";

    private static readonly Dictionary<FSMedicalBonusCategory, float> DoctrineBonuses = new()
    {
        [FSMedicalBonusCategory.TreatmentSpeed] = 0.10f,
    };

    private static readonly TimeSpan MobilisationDuration = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan DirectiveCooldown = TimeSpan.FromSeconds(60);

    private static readonly FrozenDictionary<FSMedicalDirective, DirectiveDef> Directives = new Dictionary<FSMedicalDirective, DirectiveDef>()
    {
        [FSMedicalDirective.Trauma] = new DirectiveDef("fs-cmo-directive-trauma",
            new Dictionary<FSMedicalBonusCategory, float>
            {
                [FSMedicalBonusCategory.RevivalSpeed] = 0.15f,
                [FSMedicalBonusCategory.Stabilisation] = 0.15f,
                [FSMedicalBonusCategory.DefibCooldown] = 0.15f,
            }),
        [FSMedicalDirective.Pharma] = new DirectiveDef("fs-cmo-directive-pharma",
            new Dictionary<FSMedicalBonusCategory, float>
            {
                [FSMedicalBonusCategory.TreatmentSpeed] = 0.20f,
            }),
        [FSMedicalDirective.FieldOps] = new DirectiveDef("fs-cmo-directive-fieldops",
            new Dictionary<FSMedicalBonusCategory, float>
            {
                [FSMedicalBonusCategory.Movement] = 0.10f,
                [FSMedicalBonusCategory.DragSpeed] = 0.25f,
                [FSMedicalBonusCategory.InterruptionResistance] = 0.20f,
            }),
    }.ToFrozenDictionary();

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

    // Every write goes through here. The panels are the only transport for this value, so a write
    // that forgot to sync would leave every client showing a directive that is not running.
    private void SetActiveDirective(FSMedicalDirective? directive)
    {
        _activeDirective = directive;
        RefreshDoctrine();

        var query = EntityQueryEnumerator<FSCmoPanelComponent>();
        while (query.MoveNext(out var uid, out var panel))
        {
            panel.ActiveDirective = directive;
            Dirty(uid, panel);
        }
    }

    // Reused rather than reallocated: ApplyBuff copies the dictionary, so one scratch is safe.
    private readonly Dictionary<FSMedicalBonusCategory, float> _scaledScratch = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        SubscribeNetworkEvent<FSCmoAbilityRequestEvent>(OnPanelRequest);
        SubscribeLocalEvent<FSResearchNodeCompletedEvent>(OnResearchCompleted);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        SetActiveDirective(null);
    }

    // Triage Doctrine rides whatever order is standing rather than being a permanent freebie, so
    // the CMO has to keep issuing directives to get value out of it.
    private void OnResearchCompleted(FSResearchNodeCompletedEvent ev)
    {
        if (ev.NodeId == FSMedicalUpgradeSystem.DepartmentDividend)
        {
            var panels = EntityQueryEnumerator<FSCmoPanelComponent>();
            while (panels.MoveNext(out var uid, out var panel))
            {
                panel.DividendUnlocked = true;
                Dirty(uid, panel);
            }
        }

        if (ev.NodeId != FSMedicalUpgradeSystem.TriageDoctrine)
            return;

        RefreshDoctrine();
    }

    private void RefreshDoctrine()
    {
        if (!_upgrades.Unlocked(FSMedicalUpgradeSystem.TriageDoctrine))
            return;

        if (_activeDirective == null)
            RemoveFromDepartment(DoctrineSource);
        else
            ApplyToDepartment(DoctrineSource, DoctrineBonuses, name: Loc.GetString("fs-cmo-doctrine-short"));
    }

    private void OnPanelRequest(FSCmoAbilityRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } cmo || !HasComp<FSCmoPanelComponent>(cmo))
            return;

        switch (ev.Ability)
        {
            case FSCmoAbility.MassCasualtyProtocol:
                if (Ready(cmo, mcp: true))
                    RunMassCasualtyProtocol(cmo);
                break;
            case FSCmoAbility.Mobilisation:
                if (Ready(cmo, mcp: false))
                    RunMobilisation(cmo);
                break;
            case FSCmoAbility.DirectiveTrauma:
                RunDirective(cmo, FSMedicalDirective.Trauma);
                break;
            case FSCmoAbility.DirectivePharma:
                RunDirective(cmo, FSMedicalDirective.Pharma);
                break;
            case FSCmoAbility.DirectiveFieldOps:
                RunDirective(cmo, FSMedicalDirective.FieldOps);
                break;
            case FSCmoAbility.Dividend:
                RunDividend(cmo);
                break;
        }
    }

    private bool Ready(EntityUid cmo, bool mcp)
    {
        if (!TryComp<FSCmoPanelComponent>(cmo, out var panel))
            return false;

        var readyAt = mcp ? panel.McpReadyAt : panel.MobilisationReadyAt;
        return readyAt <= _timing.CurTime;
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == FSMedicalRosterSystem.CmoJob)
        {
            var panel = EnsureComp<FSCmoPanelComponent>(ev.Mob);
            panel.ActiveDirective = _activeDirective;
            panel.DividendUnlocked = _upgrades.Unlocked(FSMedicalUpgradeSystem.DepartmentDividend);
            Dirty(ev.Mob, panel);
        }

        if (_activeDirective != null
            && _upgrades.Unlocked(FSMedicalUpgradeSystem.TriageDoctrine)
            && _roster.IsMedical(ev.Mob))
        {
            _bonus.ApplyBuff(ev.Mob, DoctrineSource, DoctrineBonuses, name: Loc.GetString("fs-cmo-doctrine-short"));
        }

        if (_activeDirective is { } active
            && Directives.TryGetValue(active, out var def)
            && _roster.IsMedical(ev.Mob))
        {
            _bonus.ApplyBuff(ev.Mob, DirectiveSource, Scaled(def.Bonuses),
                name: Loc.GetString($"{def.Announcement}-short"));
        }
    }

    private const int DividendFundCost = 1500;
    private const int DividendPayout = 1200;
    private static readonly TimeSpan DividendCooldown = TimeSpan.FromSeconds(300);

    // Deliberately a loss on conversion: this exists so a finished tree is not dead weight, not as
    // an income stream. Split across the department so it is a department reward, not the CMO's.
    private void RunDividend(EntityUid performer)
    {
        if (!_upgrades.Unlocked(FSMedicalUpgradeSystem.DepartmentDividend))
            return;

        if (!TryComp<FSCmoPanelComponent>(performer, out var panel) || panel.DividendReadyAt > _timing.CurTime)
            return;

        var recipients = new List<EntityUid>();
        foreach (var (_, mob) in _roster.Medics())
        {
            if (_mind.TryGetMind(mob, out var mindId, out _))
                recipients.Add(mindId);
        }

        if (recipients.Count == 0 || !_fund.TryDeductMedicalFunds(DividendFundCost))
            return;

        var each = DividendPayout / recipients.Count;
        foreach (var mindId in recipients)
            _wallet.GiveCredits(mindId, each);

        panel.DividendReadyAt = _timing.CurTime + DividendCooldown;
        Dirty(performer, panel);

        Announce(performer, "fs-cmo-dividend");

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(performer):cmo} paid a department dividend of {each} to {recipients.Count} medics");
    }

    private void RunMassCasualtyProtocol(EntityUid performer)
    {
        ApplyToDepartment(McpSource, Scaled(McpBonuses), McpDuration(), Loc.GetString("fs-cmo-mcp-short"));
        Announce(performer, "fs-cmo-mcp");

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(performer):cmo} ran Mass Casualty Protocol");

        if (TryComp<FSCmoPanelComponent>(performer, out var panel))
        {
            panel.McpReadyAt = _timing.CurTime + McpCooldown();
            Dirty(performer, panel);
        }
    }

    private void RunMobilisation(EntityUid performer)
    {
        ApplyToDepartment(MobilisationSource, Scaled(MobilisationBonuses), MobilisationDuration,
            Loc.GetString("fs-cmo-mobilisation-short"));
        Announce(performer, "fs-cmo-mobilisation");

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(performer):cmo} ran Mobilisation");

        if (TryComp<FSCmoPanelComponent>(performer, out var panel))
        {
            panel.MobilisationReadyAt = _timing.CurTime + MobilisationCooldown();
            Dirty(performer, panel);
        }
    }

    private void RunDirective(EntityUid performer, FSMedicalDirective directive)
    {
        if (!Directives.TryGetValue(directive, out var def))
            return;

        TryComp<FSCmoPanelComponent>(performer, out var panel);

        if (_activeDirective == directive)
        {
            SetActiveDirective(null);
            RemoveFromDepartment(DirectiveSource);
            Announce(performer, "fs-cmo-directive-stand-down", standDown: true);

            if (panel != null)
            {
                panel.DirectiveReadyAt = _timing.CurTime + DirectiveCooldown;
                Dirty(performer, panel);
            }

            _adminLogger.Add(LogType.Action, LogImpact.Low,
                $"{ToPrettyString(performer):cmo} stood down directive {directive}");
            return;
        }

        if (panel != null && panel.DirectiveReadyAt > _timing.CurTime)
            return;

        SetActiveDirective(directive);
        ApplyToDepartment(DirectiveSource, Scaled(def.Bonuses), name: Loc.GetString($"{def.Announcement}-short"));
        Announce(performer, def.Announcement);

        if (panel != null)
        {
            panel.DirectiveReadyAt = _timing.CurTime + DirectiveCooldown;
            Dirty(performer, panel);
        }

        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(performer):cmo} issued directive {directive}");
    }

    private void ApplyToDepartment(string source, Dictionary<FSMedicalBonusCategory, float> bonuses,
        TimeSpan? duration = null, string? name = null)
    {
        foreach (var (_, mob) in _roster.Medics())
            _bonus.ApplyBuff(mob, source, bonuses, duration, name);
    }

    private void RemoveFromDepartment(string source)
    {
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is { } mob)
                _bonus.RemoveBuff(mob, source);
        }
    }

    private void Announce(EntityUid performer, string key, bool standDown = false)
    {
        var name = Loc.GetString($"{key}-name");
        var effects = Loc.GetString($"{key}-effects");

        var markup = $"[fsability name=\"{name}\" tooltip=\"{effects}\"/]";
        _radio.SendRadioMessage(performer, markup, MedicalChannel, performer, escapeMarkup: false);

        var sound = standDown ? StandDownSound : DirectiveSound;

        foreach (var (session, mob) in _roster.Medics())
        {
            if (mob != performer)
                _popup.PopupEntity(name, mob, mob, PopupType.Medium);

            _audio.PlayGlobal(sound, session);
        }
    }

    // Research-bought command upgrades. Standing Orders lifts every order; the rest buy tempo.
    private Dictionary<FSMedicalBonusCategory, float> Scaled(Dictionary<FSMedicalBonusCategory, float> bonuses)
    {
        if (!_upgrades.Unlocked(FSMedicalUpgradeSystem.StandingOrders))
            return bonuses;

        _scaledScratch.Clear();
        foreach (var (category, value) in bonuses)
            _scaledScratch[category] = value * 1.25f;

        return _scaledScratch;
    }

    private TimeSpan McpDuration() =>
        TimeSpan.FromSeconds(_upgrades.Unlocked(FSMedicalUpgradeSystem.ExtendedProtocol) ? 70 : 45);

    private TimeSpan McpCooldown() =>
        TimeSpan.FromSeconds(_upgrades.Unlocked(FSMedicalUpgradeSystem.MassCasualtyReadiness) ? 80 : 120);

    private TimeSpan MobilisationCooldown() =>
        TimeSpan.FromSeconds(_upgrades.Unlocked(FSMedicalUpgradeSystem.RapidMobilisation) ? 180 : 260);

    private readonly record struct DirectiveDef(string Announcement, Dictionary<FSMedicalBonusCategory, float> Bonuses);
}
