using System.Linq;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.GameTicking.Events;
using Content.Server.Popups;
using Content.Shared._FinalStand.Research;
using Content.Shared._FinalStand.Research.Components;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared._FinalStand.Research.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Materials;
using Content.Shared.Research.Components;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Research;

// Owns the server-wide research singleton: node selection, RP accumulation, and completion.
public sealed class FSResearchSystem : SharedFSResearchSystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMaterialStorageSystem _materials = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string ResearchDirectorJob = "ResearchDirector";
    private const string CaptainJob = "Captain";
    private const string ScienceDepartment = "Science";

    private EntityUid? _station;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<FSStationResearchComponent, EntityTerminatingEvent>(OnStationTerminating);
        SubscribeLocalEvent<FSTechDatabaseComponent, ResearchRegistrationChangedEvent>(OnConsoleServerLinkChanged);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);

        Subs.BuiEvents<FSTechDatabaseComponent>(ResearchConsoleUiKey.Key, subs =>
        {
            subs.Event<FSSelectResearchNodeMessage>(OnSelectResearchNode);
        });
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        RefreshRdActivity();

        var station = GetOrCreateStation();
        var unlocked = station.Comp.UnlockedNodes.Select(n => n.Id).ToHashSet();
        RaiseNetworkEvent(new FSResearchUnlocksChangedEvent(unlocked), Filter.SinglePlayer(args.Player));
        RaiseNetworkEvent(new FSStationRpChangedEvent(station.Comp.Points), Filter.SinglePlayer(args.Player));
    }

    private void OnPlayerDetached(PlayerDetachedEvent args) => RefreshRdActivity();

    private void RefreshRdActivity()
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (!_mind.TryGetMind(session, out var mindId, out _))
                continue;

            if (!_jobs.MindHasJobWithId(mindId, ResearchDirectorJob))
                continue;

            var station = GetOrCreateStation();
            station.Comp.RdLastSeenActive = _timing.CurTime;
            Dirty(station);
            return;
        }
    }

    private void OnWaveEnded(ref WaveEndedEvent args)
    {
        var station = GetOrCreateStation();
        GrantResearchPoints(station.Comp.WaveTrickleAmount, "wave-trickle");
    }

    private void OnRoundStarting(RoundStartingEvent args)
    {
        GetOrCreateStation();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        // Reset in place - the entity may be the real, persistent physical R&D server, not a logical-only spawn.
        _station = null;
        var query = EntityQueryEnumerator<FSStationResearchComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.UnlockedNodes.Clear();
            comp.ActiveResearch = null;
            comp.NodeProgress.Clear();
            comp.Points = 0;
            comp.RdLastSeenActive = null;
            Dirty(uid, comp);
        }

        GetOrCreateStation();
        SyncConsoles();
        BroadcastUnlockedNodes();
    }

    // The physical R&D server can be destroyed/admin-deleted mid-round - re-point consoles when that happens.
    private void OnStationTerminating(EntityUid uid, FSStationResearchComponent comp, ref EntityTerminatingEvent args)
    {
        if (_station != uid)
            return;

        _station = null;
        GetOrCreateStation();
        SyncConsoles();
    }

    private void OnConsoleServerLinkChanged(EntityUid uid, FSTechDatabaseComponent comp, ref ResearchRegistrationChangedEvent args)
    {
        _station = null;
        GetOrCreateStation();
        SyncConsoles();
    }

    // Rides on the real physical R&D server entity a console is linked to (vanilla's own shared
    // research singleton) rather than a separate logical entity. Falls back to any
    // ResearchServerComponent entity, then a nullspace entity if no physical server exists.
    public Entity<FSStationResearchComponent> GetOrCreateStation()
    {
        if (_station is { } existing && Exists(existing) && TryComp<FSStationResearchComponent>(existing, out var existingComp))
            return (existing, existingComp);

        if (TryFindLinkedServer(out var linkedServer))
        {
            var linkedComp = EnsureComp<FSStationResearchComponent>(linkedServer);
            _station = linkedServer;
            return (linkedServer, linkedComp);
        }

        var serverQuery = EntityQueryEnumerator<ResearchServerComponent>();
        if (serverQuery.MoveNext(out var serverUid, out _))
        {
            var comp = EnsureComp<FSStationResearchComponent>(serverUid);
            _station = serverUid;
            return (serverUid, comp);
        }

        var fallbackQuery = EntityQueryEnumerator<FSStationResearchComponent>();
        if (fallbackQuery.MoveNext(out var uid, out var fallbackComp))
        {
            _station = uid;
            return (uid, fallbackComp);
        }

        var station = Spawn(null, MapCoordinates.Nullspace);
        var stationComp = AddComp<FSStationResearchComponent>(station);
        _station = station;
        return (station, stationComp);
    }

    private bool TryFindLinkedServer(out EntityUid server)
    {
        var query = EntityQueryEnumerator<FSTechDatabaseComponent, ResearchClientComponent>();
        while (query.MoveNext(out _, out _, out var client))
        {
            if (client.Server is { } linked && Exists(linked) && HasComp<ResearchServerComponent>(linked))
            {
                server = linked;
                return true;
            }
        }

        server = default;
        return false;
    }

    public void SyncConsoles()
    {
        var station = GetOrCreateStation();
        var query = EntityQueryEnumerator<FSTechDatabaseComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            console.UnlockedNodes.Clear();
            console.UnlockedNodes.AddRange(station.Comp.UnlockedNodes);
            console.ActiveResearch = station.Comp.ActiveResearch;
            console.NodeProgress.Clear();
            foreach (var (id, amount) in station.Comp.NodeProgress)
                console.NodeProgress[id] = amount;
            console.Points = station.Comp.Points;
            Dirty(uid, console);
        }

        RaiseNetworkEvent(new FSStationRpChangedEvent(station.Comp.Points), Filter.Broadcast());
    }

    // RD and Captain always have authority; any other Science member only once the RD has been away past RdInactivityTimeout.
    private bool HasResearchAuthority(EntityUid player, Entity<FSStationResearchComponent> station)
    {
        if (!_mind.TryGetMind(player, out var mindId, out _))
            return false;

        if (_jobs.MindHasJobWithId(mindId, ResearchDirectorJob))
            return true;

        if (_jobs.MindHasJobWithId(mindId, CaptainJob))
            return true;

        if (!_jobs.MindTryGetJob(mindId, out var job) ||
            !_jobs.TryGetPrimaryDepartment(job.ID, out var dept) ||
            dept.ID != ScienceDepartment)
            return false;

        if (station.Comp.RdLastSeenActive is not { } lastSeen)
            return true;

        return _timing.CurTime - lastSeen >= station.Comp.RdInactivityTimeout;
    }

    private void OnSelectResearchNode(EntityUid uid, FSTechDatabaseComponent comp, FSSelectResearchNodeMessage args)
    {
        var player = args.Actor;
        if (!player.IsValid())
            return;

        if (!PrototypeManager.TryIndex<FSTechNodePrototype>(args.NodeId, out var node))
            return;

        var station = GetOrCreateStation();

        if (!HasResearchAuthority(player, station))
        {
            // Two locale keys joined with a real newline - Fluent multiline placeables don't parse here.
            var reason = Loc.GetString("fs-research-no-authority") + "\n" + Loc.GetString("fs-research-no-authority-detail");
            RaiseNetworkEvent(new FSResearchAuthorityDeniedEvent(reason), Filter.Entities(player));
            return;
        }

        if (station.Comp.UnlockedNodes.Any(u => u.Id == node.ID))
        {
            _popup.PopupEntity(Loc.GetString("fs-research-already-unlocked"), uid, player);
            return;
        }

        bool IsUnlocked(string id) => station.Comp.UnlockedNodes.Any(u => u.Id == id);

        if (!ArePrerequisitesMet(node, IsUnlocked))
        {
            _popup.PopupEntity(Loc.GetString("fs-research-prereqs-not-met"), uid, player);
            return;
        }

        var unlockedIds = station.Comp.UnlockedNodes.Select(n => n.Id).ToList();
        if (IsExclusivelyBlocked(node, unlockedIds))
        {
            _popup.PopupEntity(Loc.GetString("fs-research-exclusive-locked"), uid, player);
            return;
        }

        // TryChangeMaterialAmount pre-checks every entry before applying any, so a shortfall never partially spends.
        if (node.MaterialCost.Count > 0)
        {
            var toConsume = node.MaterialCost.ToDictionary(kv => kv.Key, kv => -kv.Value);
            if (!_materials.TryChangeMaterialAmount(uid, toConsume))
            {
                _popup.PopupEntity(Loc.GetString("fs-research-insufficient-materials"), uid, player);
                return;
            }
        }

        station.Comp.ActiveResearch = node.ID;

        // Banked RP (accrued with nothing selected) immediately reinvests into the newly-selected node.
        var banked = station.Comp.Points;
        station.Comp.Points = 0;
        Dirty(station);
        SyncConsoles();

        if (banked > 0)
            GrantResearchPoints(banked, "banked-rp-reinvest");

        _popup.PopupEntity(Loc.GetString("fs-research-selected", ("name", node.Name)), uid, player);
    }

    public void GrantResearchPoints(int amount, string source)
    {
        if (amount <= 0)
            return;

        var station = GetOrCreateStation();

        if (station.Comp.ActiveResearch is not { } activeId ||
            !PrototypeManager.TryIndex<FSTechNodePrototype>(activeId, out var node))
        {
            station.Comp.Points += amount;
            Dirty(station);
            SyncConsoles();
            return;
        }

        var current = station.Comp.NodeProgress.GetValueOrDefault(activeId.Id);
        var room = Math.Max(0, node.Cost - current);
        var applied = Math.Min(amount, room);
        var overflow = amount - applied;

        station.Comp.NodeProgress[activeId.Id] = current + applied;
        if (overflow > 0)
            station.Comp.Points += overflow;

        Dirty(station);

        if (station.Comp.NodeProgress[activeId.Id] >= node.Cost)
            CompleteActiveResearch(station, node);

        SyncConsoles();
    }

    private void CompleteActiveResearch(Entity<FSStationResearchComponent> station, FSTechNodePrototype node)
    {
        station.Comp.UnlockedNodes.Add(node.ID);
        station.Comp.ActiveResearch = null;
        Dirty(station);
        BroadcastUnlockedNodes();

        Log.Info($"[FSResearch] Completed node {node.ID}");
    }

    private void BroadcastUnlockedNodes()
    {
        var station = GetOrCreateStation();
        var unlocked = station.Comp.UnlockedNodes.Select(n => n.Id).ToHashSet();
        RaiseNetworkEvent(new FSResearchUnlocksChangedEvent(unlocked), Filter.Broadcast());
    }

    public bool IsNodeUnlocked(string nodeId)
    {
        var station = GetOrCreateStation();
        return IsNodeUnlocked((station.Owner, station.Comp), nodeId);
    }
}
