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
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Research;

// Owns the server-wide research singleton: node selection, RP accumulation, and completion.
public sealed class FSResearchSystem : SharedFSResearchSystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMaterialStorageSystem _materials = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

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
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);

        Subs.BuiEvents<FSTechDatabaseComponent>(ResearchConsoleUiKey.Key, subs =>
        {
            subs.Event<FSSelectResearchNodeMessage>(OnSelectResearchNode);
            subs.Event<FSClearPersonalResearchMessage>(OnClearPersonalResearch);
        });
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        var station = GetOrCreateStation();
        var unlocked = station.Comp.UnlockedNodes.Select(n => n.Id).ToHashSet();
        RaiseNetworkEvent(new FSResearchUnlocksChangedEvent(unlocked), Filter.SinglePlayer(args.Player));
        RaiseNetworkEvent(new FSStationRpChangedEvent(station.Comp.Points), Filter.SinglePlayer(args.Player));

        if (_mind.TryGetMind(args.Player, out var mindId, out _))
            SendPersonalResearchState(mindId);
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
            comp.PersonalPicks.Clear();
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

        var contributorCounts = new Dictionary<string, int>();
        foreach (var nodeId in station.Comp.PersonalPicks.Values)
            contributorCounts[nodeId.Id] = contributorCounts.GetValueOrDefault(nodeId.Id) + 1;

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
            console.PersonalContributorCounts.Clear();
            foreach (var (id, count) in contributorCounts)
                console.PersonalContributorCounts[id] = count;
            Dirty(uid, console);
        }

        RaiseNetworkEvent(new FSStationRpChangedEvent(station.Comp.Points), Filter.Broadcast());
    }

    // RD/Captain picks become the shared, discounted default; any other Science member can still set their own personal pick.
    private bool IsRdOrCaptain(EntityUid player)
    {
        return _mind.TryGetMind(player, out var mindId, out _) &&
               (_jobs.MindHasJobWithId(mindId, ResearchDirectorJob) || _jobs.MindHasJobWithId(mindId, CaptainJob));
    }

    private bool IsScienceDepartment(EntityUid player)
    {
        return _mind.TryGetMind(player, out var mindId, out _) &&
               _jobs.MindTryGetJob(mindId, out var job) &&
               _jobs.TryGetPrimaryDepartment(job.ID, out var dept) &&
               dept.ID == ScienceDepartment;
    }

    private void OnSelectResearchNode(EntityUid uid, FSTechDatabaseComponent comp, FSSelectResearchNodeMessage args)
    {
        var player = args.Actor;
        if (!player.IsValid())
            return;

        if (!PrototypeManager.TryIndex<FSTechNodePrototype>(args.NodeId, out var node))
            return;

        var station = GetOrCreateStation();

        var isRdOrCaptain = IsRdOrCaptain(player);
        if (!isRdOrCaptain && !IsScienceDepartment(player))
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
        var alreadyStarted = station.Comp.NodeProgress.ContainsKey(node.ID);
        if (node.MaterialCost.Count > 0 && !alreadyStarted)
        {
            var toConsume = node.MaterialCost.ToDictionary(kv => kv.Key, kv => -kv.Value);
            if (!_materials.TryChangeMaterialAmount(uid, toConsume))
            {
                _popup.PopupEntity(Loc.GetString("fs-research-insufficient-materials"), uid, player);
                return;
            }
        }
        if (!alreadyStarted)
            station.Comp.NodeProgress[node.ID] = 0;

        if (isRdOrCaptain)
        {
            station.Comp.ActiveResearch = node.ID;

            // Banked RP (accrued with nothing selected) immediately reinvests into the newly-selected shared pick.
            var banked = station.Comp.Points;
            station.Comp.Points = 0;
            Dirty(station);
            SyncConsoles();

            if (banked > 0)
                GrantResearchPoints(banked, "banked-rp-reinvest");
        }
        else
        {
            if (!_mind.TryGetMind(player, out var mindId, out _))
                return;

            station.Comp.PersonalPicks[mindId] = node.ID;
            Dirty(station);
            SyncConsoles();
            SendPersonalResearchState(mindId);
        }

        _popup.PopupEntity(Loc.GetString("fs-research-selected", ("name", node.Name)), uid, player);
    }

    private void OnClearPersonalResearch(EntityUid uid, FSTechDatabaseComponent comp, FSClearPersonalResearchMessage args)
    {
        var player = args.Actor;
        if (!player.IsValid() || !_mind.TryGetMind(player, out var mindId, out _))
            return;

        var station = GetOrCreateStation();
        if (!station.Comp.PersonalPicks.Remove(mindId))
            return;

        Dirty(station);
        SendPersonalResearchState(mindId);
    }

    public void GrantResearchPoints(int amount, string source, EntityUid? contributorMindId = null)
    {
        if (amount <= 0)
            return;

        var station = GetOrCreateStation();

        ProtoId<FSTechNodePrototype>? targetId = null;
        if (contributorMindId is { } contributor &&
            station.Comp.PersonalPicks.TryGetValue(contributor, out var personalId) &&
            !station.Comp.UnlockedNodes.Any(u => u.Id == personalId.Id))
        {
            targetId = personalId;
        }
        else if (station.Comp.ActiveResearch is { } activeId &&
            !station.Comp.UnlockedNodes.Any(u => u.Id == activeId.Id))
        {
            targetId = activeId;
        }

        if (targetId is not { } id || !PrototypeManager.TryIndex<FSTechNodePrototype>(id, out var node))
        {
            station.Comp.Points += amount;
            Dirty(station);
            SyncConsoles();
            return;
        }

        var discounted = id.Id == station.Comp.ActiveResearch?.Id;
        var effectiveCost = discounted ? Math.Max(1, node.Cost / 2) : node.Cost;

        var current = station.Comp.NodeProgress.GetValueOrDefault(id.Id);
        var room = Math.Max(0, effectiveCost - current);
        var applied = Math.Min(amount, room);
        var overflow = amount - applied;

        station.Comp.NodeProgress[id.Id] = current + applied;
        if (overflow > 0)
            station.Comp.Points += overflow;

        Dirty(station);

        if (station.Comp.NodeProgress[id.Id] >= effectiveCost)
            CompleteResearch(station, node, discounted, contributorMindId);

        SyncConsoles();

        if (!discounted && contributorMindId is { } cid)
            SendPersonalResearchState(cid);
    }

    private void CompleteResearch(Entity<FSStationResearchComponent> station, FSTechNodePrototype node, bool wasShared, EntityUid? contributorMindId)
    {
        station.Comp.UnlockedNodes.Add(node.ID);

        if (wasShared)
        {
            station.Comp.ActiveResearch = null;

            var stale = station.Comp.PersonalPicks.Where(kv => kv.Value.Id == node.ID).Select(kv => kv.Key).ToList();
            foreach (var mind in stale)
                station.Comp.PersonalPicks.Remove(mind);
        }
        else if (contributorMindId is { } mind)
        {
            station.Comp.PersonalPicks.Remove(mind);
        }

        Dirty(station);
        BroadcastUnlockedNodes();
        RaiseLocalEvent(new FSResearchNodeCompletedEvent(node.ID));

        Log.Info($"[FSResearch] Completed node {node.ID}");
    }

    private void SendPersonalResearchState(EntityUid mindId)
    {
        if (!TryComp<MindComponent>(mindId, out var mindComp) || !_playerManager.TryGetSessionById(mindComp.UserId, out var session))
            return;

        var station = GetOrCreateStation();
        ProtoId<FSTechNodePrototype>? nodeId = station.Comp.PersonalPicks.TryGetValue(mindId, out var picked) ? (ProtoId<FSTechNodePrototype>?)picked : null;
        var progress = nodeId is { } id ? station.Comp.NodeProgress.GetValueOrDefault(id.Id) : 0;
        RaiseNetworkEvent(new FSPersonalResearchStateEvent(nodeId, progress), Filter.SinglePlayer(session));
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
