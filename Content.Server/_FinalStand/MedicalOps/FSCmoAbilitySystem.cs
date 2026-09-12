using System.Collections.Frozen;
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
    [Dependency] private FSMedicalRolesSystem _roles = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier DirectiveSound =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/directive.ogg");

    private static readonly SoundSpecifier StandDownSound =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/directive_standdown.ogg");

    private static readonly ProtoId<RadioChannelPrototype> MedicalChannel = "Medical";

    private const string McpSource = "mcp";
    private const string DirectiveSource = "directive";
    private const string MobilisationSource = "mobilisation";

    private const string CmoJob = "ChiefMedicalOfficer";

    private static readonly TimeSpan McpDuration = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan MobilisationDuration = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan McpCooldown = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan MobilisationCooldown = TimeSpan.FromSeconds(260);
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

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        SubscribeNetworkEvent<FSCmoAbilityRequestEvent>(OnPanelRequest);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _activeDirective = null;
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
        }
    }

    private bool Ready(EntityUid cmo, bool mcp)
    {
        if (!TryComp<FSCmoPanelComponent>(cmo, out var panel))
            return false;

        var readyAt = mcp ? panel.McpReadyAt : panel.MobilisationReadyAt;
        return readyAt <= _timing.CurTime;
    }

    private void SyncPanels()
    {
        var query = EntityQueryEnumerator<FSCmoPanelComponent>();
        while (query.MoveNext(out var uid, out var panel))
        {
            panel.ActiveDirective = _activeDirective;
            Dirty(uid, panel);
        }
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == CmoJob)
        {
            var panel = EnsureComp<FSCmoPanelComponent>(ev.Mob);
            panel.ActiveDirective = _activeDirective;
            Dirty(ev.Mob, panel);
        }

        if (_activeDirective is { } active
            && Directives.TryGetValue(active, out var def)
            && _roles.IsMedicalStaff(ev.Mob))
        {
            _bonus.ApplyBuff(ev.Mob, DirectiveSource, def.Bonuses,
                name: Loc.GetString($"{def.Announcement}-short"));
        }
    }

    private void RunMassCasualtyProtocol(EntityUid performer)
    {
        ApplyToDepartment(McpSource, McpBonuses, McpDuration, Loc.GetString("fs-cmo-mcp-short"));
        Announce(performer, "fs-cmo-mcp");

        if (TryComp<FSCmoPanelComponent>(performer, out var panel))
        {
            panel.McpReadyAt = _timing.CurTime + McpCooldown;
            Dirty(performer, panel);
        }
    }

    private void RunMobilisation(EntityUid performer)
    {
        ApplyToDepartment(MobilisationSource, MobilisationBonuses, MobilisationDuration,
            Loc.GetString("fs-cmo-mobilisation-short"));
        Announce(performer, "fs-cmo-mobilisation");

        if (TryComp<FSCmoPanelComponent>(performer, out var panel))
        {
            panel.MobilisationReadyAt = _timing.CurTime + MobilisationCooldown;
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
            _activeDirective = null;
            RemoveFromDepartment(DirectiveSource);
            Announce(performer, "fs-cmo-directive-stand-down", standDown: true);
            SyncPanels();
            return;
        }

        if (panel != null && panel.DirectiveReadyAt > _timing.CurTime)
            return;

        _activeDirective = directive;
        ApplyToDepartment(DirectiveSource, def.Bonuses, name: Loc.GetString($"{def.Announcement}-short"));
        Announce(performer, def.Announcement);

        if (panel != null)
        {
            panel.DirectiveReadyAt = _timing.CurTime + DirectiveCooldown;
            Dirty(performer, panel);
        }

        SyncPanels();
    }

    private void ApplyToDepartment(string source, Dictionary<FSMedicalBonusCategory, float> bonuses,
        TimeSpan? duration = null, string? name = null)
    {
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is { } mob && _roles.IsMedicalStaff(mob))
                _bonus.ApplyBuff(mob, source, bonuses, duration, name);
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

    private void Announce(EntityUid performer, string key, bool standDown = false)
    {
        var name = Loc.GetString($"{key}-name");
        var effects = Loc.GetString($"{key}-effects");

        var markup = $"[fsability name=\"{name}\" tooltip=\"{effects}\"/]";
        _radio.SendRadioMessage(performer, markup, MedicalChannel, performer, escapeMarkup: false);

        var sound = standDown ? StandDownSound : DirectiveSound;

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } mob || !_roles.IsMedicalStaff(mob))
                continue;

            if (mob != performer)
                _popup.PopupEntity(name, mob, mob, PopupType.Medium);

            _audio.PlayGlobal(sound, session);
        }
    }

    private readonly record struct DirectiveDef(string Announcement, Dictionary<FSMedicalBonusCategory, float> Bonuses);
}
