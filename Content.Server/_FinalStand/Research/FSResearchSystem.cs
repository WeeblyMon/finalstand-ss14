using System.Linq;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.GameTicking.Events;
using Content.Server.Popups;
using Content.Shared._FinalStand.Research;
using Content.Shared._FinalStand.Research.Components;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared._FinalStand.Research.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Systems;
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
public sealed partial class FSResearchSystem : SharedFSResearchSystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedMaterialStorageSystem _materials = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private Science.FSScienceOnlySystem _scienceOnly = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private static readonly ProtoId<AccessLevelPrototype> ResearchDirectorAccess = "ResearchDirector";
    private static readonly ProtoId<AccessLevelPrototype> CaptainAccess = "Captain";

    private EntityUid? _station;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<FSStationResearchComponent, EntityTerminatingEvent>(OnStationTerminating);
        SubscribeLocalEvent<FSTechDatabaseComponent, ResearchRegistrationChangedEvent>(OnConsoleServerLinkChanged);
        SubscribeLocalEvent<FSTechDatabaseComponent, GetMaterialWhitelistEvent>(OnGetMaterialWhitelist);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);

        Subs.BuiEvents<FSTechDatabaseComponent>(ResearchConsoleUiKey.Key, subs =>
        {
            subs.Event<FSSelectResearchNodeMessage>(OnSelectResearchNode);
            subs.Event<FSEnqueueResearchNodeMessage>(OnEnqueueResearchNode);
            subs.Event<FSDequeueResearchNodeMessage>(OnDequeueResearchNode);
            subs.Event<FSClearPersonalResearchMessage>(OnClearPersonalResearch);
            subs.Event<FSClearSharedResearchMessage>(OnClearSharedResearch);
        });
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        var station = GetOrCreateStation();
        var unlocked = station.Comp.UnlockedNodes.Select(n => n.Id).ToHashSet();
        RaiseNetworkEvent(new FSResearchUnlocksChangedEvent(unlocked), Filter.SinglePlayer(args.Player));
        RaiseNetworkEvent(new FSStationRpChangedEvent(station.Comp.Points), Filter.SinglePlayer(args.Player));
        RaiseNetworkEvent(new FSPlayerResearchAuthorityEvent(IsRdOrCaptain(args.Entity)), Filter.SinglePlayer(args.Player));

        var activeProgress = station.Comp.ActiveResearch is { } activeId
            ? station.Comp.NodeProgress.GetValueOrDefault(activeId.Id)
            : 0;
        RaiseNetworkEvent(new FSSharedResearchStateEvent(station.Comp.ActiveResearch, activeProgress), Filter.SinglePlayer(args.Player));

        if (_mind.TryGetMind(args.Player, out var mindId, out _))
            SendPersonalResearchState(mindId);
    }

    // PlayerAttachedEvent fires before MindAddJobRole runs, so IsRdOrCaptain sees no job yet there - re-check once the job is actually assigned.
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        RaiseNetworkEvent(new FSPlayerResearchAuthorityEvent(IsRdOrCaptain(args.Mob)), Filter.SinglePlayer(args.Player));
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
            ClearUnlockedNodes(comp);
            comp.ActiveResearch = null;
            comp.SharedQueue.Clear();
            comp.NodeProgress.Clear();
            comp.Points = 0;
            comp.PersonalPicks.Clear();
            comp.PersonalQueues.Clear();
            comp.ContributorColorSlots.Clear();
            comp.ActiveResearchSetBy = null;
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

    // Vanilla stores an empty list when nothing answers, and empty rejects every material.
    private void OnGetMaterialWhitelist(EntityUid uid, FSTechDatabaseComponent comp, ref GetMaterialWhitelistEvent args)
    {
        if (args.Storage != uid)
            return;

        foreach (var node in PrototypeManager.EnumeratePrototypes<FSTechNodePrototype>())
        {
            foreach (var (material, _) in node.MaterialCost)
            {
                if (!args.Whitelist.Contains(material))
                    args.Whitelist.Add(material);
            }
        }
    }

    private void OnConsoleServerLinkChanged(EntityUid uid, FSTechDatabaseComponent comp, ref ResearchRegistrationChangedEvent args)
    {
        _station = null;
        GetOrCreateStation();
        SyncConsoles();
    }

    public Entity<FSStationResearchComponent> GetOrCreateStation()
    {
        if (_station is { } existing && Exists(existing) && TryComp<FSStationResearchComponent>(existing, out var existingComp))
            return (existing, existingComp);
        var fallbackQuery = EntityQueryEnumerator<FSStationResearchComponent>();
        if (fallbackQuery.MoveNext(out var uid, out var fallbackComp))
        {
            _station = uid;
            return (uid, fallbackComp);
        }

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

        var contributorSlots = new Dictionary<string, List<int>>();
        foreach (var (mindId, nodeId) in station.Comp.PersonalPicks)
        {
            var slot = GetOrAssignColorSlot(station, mindId);
            if (!contributorSlots.TryGetValue(nodeId.Id, out var slots))
                contributorSlots[nodeId.Id] = slots = new List<int>();
            slots.Add(slot);
        }

        if (station.Comp.ActiveResearch is { } activeNodeId && station.Comp.ActiveResearchSetBy is { } setterMindId)
        {
            var setterSlot = GetOrAssignColorSlot(station, setterMindId);
            if (!contributorSlots.TryGetValue(activeNodeId.Id, out var activeSlots))
                contributorSlots[activeNodeId.Id] = activeSlots = new List<int>();
            activeSlots.Add(setterSlot);
        }

        var query = EntityQueryEnumerator<FSTechDatabaseComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            console.UnlockedNodes.Clear();
            console.UnlockedNodes.AddRange(station.Comp.UnlockedNodes);
            console.ActiveResearch = station.Comp.ActiveResearch;
            console.SharedQueue.Clear();
            console.SharedQueue.AddRange(station.Comp.SharedQueue);
            console.NodeProgress.Clear();
            foreach (var (id, amount) in station.Comp.NodeProgress)
                console.NodeProgress[id] = amount;
            console.Points = station.Comp.Points;
            console.PersonalContributorSlots.Clear();
            foreach (var (id, slots) in contributorSlots)
                console.PersonalContributorSlots[id] = new List<int>(slots);
            Dirty(uid, console);
        }

        RaiseNetworkEvent(new FSStationRpChangedEvent(station.Comp.Points), Filter.Broadcast());

        var activeProgress = station.Comp.ActiveResearch is { } activeId
            ? station.Comp.NodeProgress.GetValueOrDefault(activeId.Id)
            : 0;
        RaiseNetworkEvent(new FSSharedResearchStateEvent(station.Comp.ActiveResearch, activeProgress), Filter.Broadcast());
    }

    private static int GetOrAssignColorSlot(Entity<FSStationResearchComponent> station, EntityUid mindId)
    {
        if (station.Comp.ContributorColorSlots.TryGetValue(mindId, out var slot))
            return slot;

        slot = station.Comp.ContributorColorSlots.Count;
        station.Comp.ContributorColorSlots[mindId] = slot;
        return slot;
    }

    // RD and Captain set the shared pick; checked off the held ID/PDA, not spawn job, so promotion mid-round works.
    private bool IsRdOrCaptain(EntityUid player)
    {
        var tags = _accessReader.FindAccessTags(player);
        return tags.Contains(ResearchDirectorAccess) || tags.Contains(CaptainAccess);
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
        if (!isRdOrCaptain && !_scienceOnly.IsScience(player))
        {
            // Two locale keys joined with a real newline - Fluent multiline placeables don't parse here.
            var reason = Loc.GetString("fs-research-no-authority") + "\n" + Loc.GetString("fs-research-no-authority-detail");
            RaiseNetworkEvent(new FSResearchAuthorityDeniedEvent(reason), Filter.Entities(player));
            return;
        }

        if (IsNodeUnlocked(station.Comp, node.ID))
        {
            _popup.PopupEntity(Loc.GetString("fs-research-already-unlocked"), uid, player);
            return;
        }

        bool IsUnlocked(string id) => IsNodeUnlocked(station.Comp, id);

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

            if (_mind.TryGetMind(player, out var setterMindId, out _))
                station.Comp.ActiveResearchSetBy = setterMindId;

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

    private void OnEnqueueResearchNode(EntityUid uid, FSTechDatabaseComponent comp, FSEnqueueResearchNodeMessage args)
    {
        var player = args.Actor;
        if (!player.IsValid())
            return;

        if (!PrototypeManager.TryIndex<FSTechNodePrototype>(args.NodeId, out var node))
            return;

        var station = GetOrCreateStation();

        var isRdOrCaptain = IsRdOrCaptain(player);
        if (!isRdOrCaptain && !_scienceOnly.IsScience(player))
        {
            var reason = Loc.GetString("fs-research-no-authority") + "\n" + Loc.GetString("fs-research-no-authority-detail");
            RaiseNetworkEvent(new FSResearchAuthorityDeniedEvent(reason), Filter.Entities(player));
            return;
        }

        if (IsNodeUnlocked(station.Comp, node.ID))
        {
            _popup.PopupEntity(Loc.GetString("fs-research-already-unlocked"), uid, player);
            return;
        }

        var unlockedIds = station.Comp.UnlockedNodes.Select(n => n.Id).ToList();
        if (IsExclusivelyBlocked(node, unlockedIds))
        {
            _popup.PopupEntity(Loc.GetString("fs-research-exclusive-locked"), uid, player);
            return;
        }

        if (isRdOrCaptain)
        {
            if (station.Comp.ActiveResearch?.Id == node.ID || station.Comp.SharedQueue.Any(q => q.Id == node.ID))
            {
                _popup.PopupEntity(Loc.GetString("fs-research-already-queued"), uid, player);
                return;
            }

            if (station.Comp.SharedQueue.Count >= MaxQueueLength)
            {
                _popup.PopupEntity(Loc.GetString("fs-research-queue-full", ("max", MaxQueueLength)), uid, player);
                return;
            }

            station.Comp.SharedQueue.Add(node.ID);
            AdvanceSharedQueue(station);
            Dirty(station);
            SyncConsoles();
        }
        else
        {
            if (!_mind.TryGetMind(player, out var mindId, out _))
                return;

            if (!station.Comp.PersonalQueues.TryGetValue(mindId, out var queue))
                station.Comp.PersonalQueues[mindId] = queue = new List<ProtoId<FSTechNodePrototype>>();

            var alreadyPicked = station.Comp.PersonalPicks.TryGetValue(mindId, out var current) && current.Id == node.ID;
            if (alreadyPicked || queue.Any(q => q.Id == node.ID))
            {
                _popup.PopupEntity(Loc.GetString("fs-research-already-queued"), uid, player);
                return;
            }

            if (queue.Count >= MaxQueueLength)
            {
                _popup.PopupEntity(Loc.GetString("fs-research-queue-full", ("max", MaxQueueLength)), uid, player);
                return;
            }

            queue.Add(node.ID);
            AdvancePersonalQueue(station, mindId);
            Dirty(station);
            SyncConsoles();
            SendPersonalResearchState(mindId);
        }

        _popup.PopupEntity(Loc.GetString("fs-research-queued", ("name", node.Name)), uid, player);
    }

    private void OnDequeueResearchNode(EntityUid uid, FSTechDatabaseComponent comp, FSDequeueResearchNodeMessage args)
    {
        var player = args.Actor;
        if (!player.IsValid())
            return;

        var station = GetOrCreateStation();

        if (IsRdOrCaptain(player))
        {
            var sharedIndex = station.Comp.SharedQueue.FindIndex(q => q.Id == args.NodeId);
            if (sharedIndex >= 0)
            {
                station.Comp.SharedQueue.RemoveAt(sharedIndex);
                Dirty(station);
                SyncConsoles();
                return;
            }
        }

        if (!_mind.TryGetMind(player, out var mindId, out _))
            return;

        if (!station.Comp.PersonalQueues.TryGetValue(mindId, out var queue))
            return;

        var index = queue.FindIndex(q => q.Id == args.NodeId);
        if (index < 0)
            return;

        queue.RemoveAt(index);
        Dirty(station);
        SyncConsoles();
        SendPersonalResearchState(mindId);
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
        SyncConsoles();
        SendPersonalResearchState(mindId);
    }

    private void OnClearSharedResearch(EntityUid uid, FSTechDatabaseComponent comp, FSClearSharedResearchMessage args)
    {
        var player = args.Actor;
        if (!player.IsValid() || !IsRdOrCaptain(player))
            return;

        var station = GetOrCreateStation();
        if (station.Comp.ActiveResearch == null)
            return;

        station.Comp.ActiveResearch = null;
        station.Comp.ActiveResearchSetBy = null;
        Dirty(station);
        SyncConsoles();
    }

    private enum QueueStep : byte
    {
        Start,
        Drop,
        Stall,
    }

    private QueueStep EvaluateQueued(Entity<FSStationResearchComponent> station, ProtoId<FSTechNodePrototype> id, out FSTechNodePrototype? node)
    {
        if (!PrototypeManager.TryIndex(id, out node))
            return QueueStep.Drop;

        if (IsNodeUnlocked(station.Comp, node.ID))
            return QueueStep.Drop;

        if (IsExclusivelyBlocked(node, station.Comp.UnlockedLookup))
            return QueueStep.Drop;

        if (!ArePrerequisitesMet(node, nodeId => IsNodeUnlocked(station.Comp, nodeId)))
            return QueueStep.Stall;

        return QueueStep.Start;
    }

    private bool TryChargeMaterials(Entity<FSStationResearchComponent> station, FSTechNodePrototype node)
    {
        if (node.MaterialCost.Count == 0 || station.Comp.NodeProgress.ContainsKey(node.ID))
            return true;

        var toConsume = node.MaterialCost.ToDictionary(kv => kv.Key, kv => -kv.Value);
        var query = EntityQueryEnumerator<FSTechDatabaseComponent>();
        while (query.MoveNext(out var consoleUid, out _))
        {
            if (_materials.TryChangeMaterialAmount(consoleUid, toConsume))
                return true;
        }

        return false;
    }

    private void AdvanceSharedQueue(Entity<FSStationResearchComponent> station)
    {
        if (station.Comp.ActiveResearch != null)
            return;

        while (station.Comp.SharedQueue.Count > 0)
        {
            var step = EvaluateQueued(station, station.Comp.SharedQueue[0], out var node);
            if (step == QueueStep.Drop)
            {
                station.Comp.SharedQueue.RemoveAt(0);
                continue;
            }

            if (step == QueueStep.Stall || node == null || !TryChargeMaterials(station, node))
                return;

            station.Comp.SharedQueue.RemoveAt(0);
            station.Comp.NodeProgress.TryAdd(node.ID, 0);
            station.Comp.ActiveResearch = node.ID;
            return;
        }
    }

    private void AdvancePersonalQueue(Entity<FSStationResearchComponent> station, EntityUid mindId)
    {
        if (station.Comp.PersonalPicks.ContainsKey(mindId))
            return;

        if (!station.Comp.PersonalQueues.TryGetValue(mindId, out var queue))
            return;

        while (queue.Count > 0)
        {
            var step = EvaluateQueued(station, queue[0], out var node);
            if (step == QueueStep.Drop)
            {
                queue.RemoveAt(0);
                continue;
            }

            if (step == QueueStep.Stall || node == null || !TryChargeMaterials(station, node))
                return;

            queue.RemoveAt(0);
            station.Comp.NodeProgress.TryAdd(node.ID, 0);
            station.Comp.PersonalPicks[mindId] = node.ID;
            return;
        }
    }

    public void GrantResearchPoints(int amount, string source, EntityUid? contributorMindId = null)
    {
        if (amount <= 0)
            return;

        var station = GetOrCreateStation();

        AdvanceSharedQueue(station);
        if (contributorMindId is { } retryMind)
            AdvancePersonalQueue(station, retryMind);

        ProtoId<FSTechNodePrototype>? targetId = null;
        if (contributorMindId is { } contributor &&
            station.Comp.PersonalPicks.TryGetValue(contributor, out var personalId) &&
            !IsNodeUnlocked(station.Comp, personalId.Id))
        {
            targetId = personalId;
        }
        else if (station.Comp.ActiveResearch is { } activeId &&
            !IsNodeUnlocked(station.Comp, activeId.Id))
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

        // Forward-only: node.Cost never changes, past progress keeps its original value (no retroactive sniping).
        var discounted = id.Id == station.Comp.ActiveResearch?.Id;
        var multiplier = discounted ? 2 : 1;

        var current = station.Comp.NodeProgress.GetValueOrDefault(id.Id);
        var room = Math.Max(0, node.Cost - current);
        var maxUsableRp = (room + multiplier - 1) / multiplier; // ceiling division - floor can strand the last point of room unreachable
        var applied = Math.Min(amount, maxUsableRp);
        var progressGain = applied * multiplier;
        var overflow = amount - applied;

        station.Comp.NodeProgress[id.Id] = current + progressGain;
        if (overflow > 0)
            station.Comp.Points += overflow;

        Dirty(station);

        if (station.Comp.NodeProgress[id.Id] >= node.Cost)
            CompleteResearch(station, node, discounted, contributorMindId);

        SyncConsoles();

        if (!discounted && contributorMindId is { } cid)
            SendPersonalResearchState(cid);
    }

    private void CompleteResearch(Entity<FSStationResearchComponent> station, FSTechNodePrototype node, bool wasShared, EntityUid? contributorMindId)
    {
        MarkNodeUnlocked(station.Comp, node.ID);

        if (wasShared)
        {
            station.Comp.ActiveResearch = null;
            station.Comp.ActiveResearchSetBy = null;
        }

        // The node is finished, so every pick aimed at it clears, not just the last contributor's.
        var stale = station.Comp.PersonalPicks
            .Where(kv => kv.Value.Id == node.ID)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var mind in stale)
            station.Comp.PersonalPicks.Remove(mind);

        AdvanceSharedQueue(station);
        foreach (var mind in stale)
            AdvancePersonalQueue(station, mind);

        Dirty(station);
        BroadcastUnlockedNodes();

        foreach (var mind in stale)
            SendPersonalResearchState(mind);

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
        var queue = station.Comp.PersonalQueues.TryGetValue(mindId, out var personalQueue)
            ? personalQueue.Select(n => n.Id).ToList()
            : new List<string>();
        RaiseNetworkEvent(new FSPersonalResearchStateEvent(nodeId, progress, queue), Filter.SinglePlayer(session));
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

    public int UnlockAllNodes()
    {
        var station = GetOrCreateStation();
        var alreadyUnlocked = station.Comp.UnlockedNodes.Select(n => n.Id).ToHashSet();

        var count = 0;
        foreach (var node in PrototypeManager.EnumeratePrototypes<FSTechNodePrototype>())
        {
            if (!alreadyUnlocked.Add(node.ID))
                continue;

            MarkNodeUnlocked(station.Comp, node.ID);
            RaiseLocalEvent(new FSResearchNodeCompletedEvent(node.ID));
            count++;
        }

        station.Comp.ActiveResearch = null;
        station.Comp.ActiveResearchSetBy = null;
        station.Comp.SharedQueue.Clear();
        station.Comp.PersonalPicks.Clear();
        station.Comp.PersonalQueues.Clear();
        station.Comp.ContributorColorSlots.Clear();
        Dirty(station);
        SyncConsoles();
        BroadcastUnlockedNodes();

        return count;
    }
}
