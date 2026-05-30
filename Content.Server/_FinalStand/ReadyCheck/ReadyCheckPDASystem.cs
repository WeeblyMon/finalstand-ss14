using Content.Server.CartridgeLoader;
using Content.Shared._FinalStand.ReadyCheck;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.CartridgeLoader;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.ReadyCheck;

public sealed class ReadyCheckPDASystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly ReadyCheckSystem _readyCheck = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ReadyCheckCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<ReadyCheckCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<ReadyCheckUpdatedEvent>(OnReadyCheckUpdated);
        SubscribeLocalEvent<WavePrepStartedEvent>(OnWavePrepStarted);
        SubscribeLocalEvent<WaveCombatStartedEvent>(OnWaveCombatStarted);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnWavePrepStarted(WavePrepStartedEvent ev)
    {
        var commandFilter = Filter.Empty();
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } player)
                continue;
            TryInstallCartridge(player, GetJobId(player));
            if (GetJobId(player) is { } jobId && ReadyCheckDepts.IsCommandJob(jobId))
                commandFilter.AddPlayer(session);
        }
        RaiseNetworkEvent(new WavePhaseChangedEvent(true), commandFilter);
    }

    private void OnWaveCombatStarted(WaveCombatStartedEvent ev)
    {
        RaiseNetworkEvent(new WavePhaseChangedEvent(false), CommandFilter());
    }

    private Filter CommandFilter()
    {
        var filter = Filter.Empty();
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is { } player
                && GetJobId(player) is { } jobId
                && ReadyCheckDepts.IsCommandJob(jobId))
                filter.AddPlayer(session);
        }
        return filter;
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        TryInstallCartridge(ev.Mob, ev.JobId);
    }

    private void TryInstallCartridge(EntityUid player, string? jobId = null)
    {
        jobId ??= GetJobId(player);
        if (jobId == null || !ReadyCheckDepts.IsCommandJob(jobId))
            return;
        if (!_inventory.TryGetSlotEntity(player, "id", out var idUid))
            return;
        if (!HasComp<CartridgeLoaderComponent>(idUid.Value))
            return;
        if (!_cartridgeLoader.TryGetProgram<ReadyCheckCartridgeComponent>(idUid.Value, out _, out _))
            _cartridgeLoader.InstallProgram(idUid.Value, "ReadyCheckCartridge", deinstallable: false);
    }

    private void OnUiReady(EntityUid uid, ReadyCheckCartridgeComponent comp, CartridgeUiReadyEvent args)
    {
        var actor = FindPDAHolder(args.Loader);
        if (actor == null)
            return;
        PushStateToLoader(args.Loader, actor.Value);
    }

    private void OnUiMessage(EntityUid uid, ReadyCheckCartridgeComponent comp, CartridgeMessageEvent args)
    {
        if (args is not ReadyCheckUiMessageEvent msg)
            return;

        var jobId = GetJobId(args.Actor);
        if (jobId == null || !ReadyCheckDepts.HeadJobToDisplay.TryGetValue(jobId, out var deptCode))
            return;

        _readyCheck.SetDepartmentStatus(deptCode, msg.NewStatus);
    }

    private void OnReadyCheckUpdated(ReadyCheckUpdatedEvent ev)
    {
        PushStateToAllPDAs();
    }

    private void PushStateToAllPDAs()
    {
        var statuses = _readyCheck.GetStatuses();
        var isCombat = _readyCheck.IsCombatPhase();

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } player)
                continue;
            if (!_inventory.TryGetSlotEntity(player, "id", out var idUid))
                continue;
            if (!HasComp<CartridgeLoaderComponent>(idUid.Value))
                continue;
            if (!_cartridgeLoader.TryGetProgram<ReadyCheckCartridgeComponent>(idUid.Value, out _, out _))
                continue;
            PushStateToLoader(idUid.Value, player, statuses, isCombat);
        }
    }

    private void PushStateToLoader(
        EntityUid loaderUid,
        EntityUid actor,
        Dictionary<string, ReadyStatus>? statuses = null,
        bool? isCombat = null)
    {
        statuses ??= _readyCheck.GetStatuses();
        isCombat ??= _readyCheck.IsCombatPhase();

        var jobId = GetJobId(actor);
        var isCommand = jobId != null && ReadyCheckDepts.IsCommandJob(jobId);
        var isCaptain = jobId != null && ReadyCheckDepts.IsCaptain(jobId);
        string? myDept = jobId != null && ReadyCheckDepts.HeadJobToDisplay.TryGetValue(jobId, out var d) ? d : null;
        var myStatus = myDept != null && statuses.TryGetValue(myDept, out var s) ? s : ReadyStatus.NoResponse;

        var state = new ReadyCheckPDAUiState(myStatus, statuses, isCombat.Value, isCommand, isCaptain);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

    private EntityUid? FindPDAHolder(EntityUid pdaUid)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } player)
                continue;
            if (!_inventory.TryGetSlotEntity(player, "id", out var idUid))
                continue;
            if (idUid.Value == pdaUid)
                return player;
        }
        return null;
    }

    private string? GetJobId(EntityUid player)
    {
        if (!_mind.TryGetMind(player, out var mindId, out _))
            return null;
        return _jobs.MindTryGetJob(mindId, out var job) ? job.ID : null;
    }
}
