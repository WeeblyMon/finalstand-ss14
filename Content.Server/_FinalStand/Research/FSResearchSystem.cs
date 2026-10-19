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
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Research;

public sealed partial class FSResearchSystem : SharedFSResearchSystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedMaterialStorageSystem _materials = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private Science.FSScienceOnlySystem _scienceOnly = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private FSMedicalResearchSystem _medicalResearch = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<AccessLevelPrototype> ResearchDirectorAccess = "ResearchDirector";
    private static readonly ProtoId<AccessLevelPrototype> CaptainAccess = "Captain";

    private EntityUid? _station;

    // Weapons like the Harvester grant points on every hit, so plain progress is batched instead of synced per hit.
    private static readonly TimeSpan ProgressSyncInterval = TimeSpan.FromSeconds(0.25);
    private TimeSpan _nextProgressSync;
    private bool _progressSyncPending;

    // Set when the shared queue starts a node, so banked RP (and overflow from the node that just finished)
    // goes into it. Done next tick because the queue advances from inside the grant itself.
    private bool _reinvestPending;
    private readonly HashSet<EntityUid> _personalSyncPending = new();

    // Last values broadcast to every client; late joiners get theirs on attach.
    private int? _sentPoints;
    private bool _sentShared;
    private ProtoId<FSTechNodePrototype>? _sentActive;
    private int _sentActiveProgress;

    public override void Initialize()
    {
        base.Initialize();

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
        var hasStation = TryGetStation(out var station);
        var unlocked = hasStation ? station.Comp.UnlockedNodes.Select(n => n.Id).ToHashSet() : new HashSet<string>();
        unlocked.UnionWith(_medicalResearch.UnlockedNodeIds());
        RaiseNetworkEvent(new FSResearchUnlocksChangedEvent(unlocked), Filter.SinglePlayer(args.Player));
        RaiseNetworkEvent(new FSStationRpChangedEvent(hasStation ? station.Comp.Points : 0), Filter.SinglePlayer(args.Player));
        RaiseNetworkEvent(new FSPlayerResearchAuthorityEvent(IsRdOrCaptain(args.Entity)), Filter.SinglePlayer(args.Player));

        var active = hasStation ? station.Comp.ActiveResearch : null;
        var activeProgress = active is { } activeId
            ? station.Comp.NodeProgress.GetValueOrDefault(activeId.Id)
            : 0;
        RaiseNetworkEvent(new FSSharedResearchStateEvent(active, activeProgress), Filter.SinglePlayer(args.Player));

        if (_mind.TryGetMind(args.Player, out var mindId, out _))
            SendPersonalResearchState(mindId);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        RaiseNetworkEvent(new FSPlayerResearchAuthorityEvent(IsRdOrCaptain(args.Mob)), Filter.SinglePlayer(args.Player));
    }

    private void OnWaveEnded(ref WaveEndedEvent args)
    {
        var station = GetOrCreateStation();
        GrantResearchPoints(station.Comp.WaveTrickleAmount, "wave-trickle");
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _station = null;
        _progressSyncPending = false;
        _personalSyncPending.Clear();
        _sentPoints = null;
        _sentShared = false;
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

        SyncConsoles();
        BroadcastUnlockedNodes();
    }

    private void OnStationTerminating(EntityUid uid, FSStationResearchComponent comp, ref EntityTerminatingEvent args)
    {
        if (_station != uid)
            return;

        _station = null;
    }

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
        if (TerminatingOrDeleted(uid))
            return;

        if (args.Server is { } server && !TerminatingOrDeleted(server))
            GetOrCreateStation();
        SyncConsoles();
    }

    public Entity<FSStationResearchComponent> GetOrCreateStation()
    {
        if (TryGetStation(out var found))
            return found;

        var station = Spawn(null, MapCoordinates.Nullspace);
        var stationComp = AddComp<FSStationResearchComponent>(station);
        _station = station;
        return (station, stationComp);
    }

    public bool TryGetStation(out Entity<FSStationResearchComponent> station)
    {
        station = default;

        if (_station is { } existing && Exists(existing) && TryComp<FSStationResearchComponent>(existing, out var existingComp))
        {
            station = (existing, existingComp);
            return true;
        }

        var fallbackQuery = EntityQueryEnumerator<FSStationResearchComponent>();
        while (fallbackQuery.MoveNext(out var uid, out var fallbackComp))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            _station = uid;
            station = (uid, fallbackComp);
            return true;
        }

        if (TryFindLinkedServer(out var linkedServer))
        {
            var linkedComp = EnsureComp<FSStationResearchComponent>(linkedServer);
            _station = linkedServer;
            station = (linkedServer, linkedComp);
            return true;
        }

        var serverQuery = EntityQueryEnumerator<ResearchServerComponent>();
        while (serverQuery.MoveNext(out var serverUid, out _))
        {
            if (TerminatingOrDeleted(serverUid))
                continue;

            var comp = EnsureComp<FSStationResearchComponent>(serverUid);
            _station = serverUid;
            station = (serverUid, comp);
            return true;
        }

        return false;
    }

    private bool TryFindLinkedServer(out EntityUid server)
    {
        var query = EntityQueryEnumerator<FSTechDatabaseComponent, ResearchClientComponent>();
        while (query.MoveNext(out _, out _, out var client))
        {
            if (client.Server is { } linked && !TerminatingOrDeleted(linked) && HasComp<ResearchServerComponent>(linked))
            {
                server = linked;
                return true;
            }
        }

        server = default;
        return false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_reinvestPending)
        {
            _reinvestPending = false;
            if (TryGetStation(out var station) && station.Comp.ActiveResearch != null && station.Comp.Points > 0)
            {
                var banked = station.Comp.Points;
                station.Comp.Points = 0;
                GrantResearchPoints(banked, "banked-rp-reinvest");
            }
        }

        if (!_progressSyncPending && _personalSyncPending.Count == 0 || _timing.CurTime < _nextProgressSync)
            return;

        _nextProgressSync = _timing.CurTime + ProgressSyncInterval;
        if (_progressSyncPending)
            SyncConsoles();

        foreach (var mind in _personalSyncPending)
            SendPersonalResearchState(mind);

        _personalSyncPending.Clear();
    }

    public void SyncConsoles()
    {
        _progressSyncPending = false;
        if (!TryGetStation(out var station))
            return;

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
            if (console.Track == FSResearchTrack.Science)
                SyncConsole((uid, console), station.Comp, contributorSlots);
        }

        // Clients already hold the last values; resending them at the progress rate is pure traffic.
        if (_sentPoints != station.Comp.Points)
        {
            _sentPoints = station.Comp.Points;
            RaiseNetworkEvent(new FSStationRpChangedEvent(station.Comp.Points), Filter.Broadcast());
        }

        var activeProgress = station.Comp.ActiveResearch is { } activeId
            ? station.Comp.NodeProgress.GetValueOrDefault(activeId.Id)
            : 0;
        if (!_sentShared || _sentActive != station.Comp.ActiveResearch || _sentActiveProgress != activeProgress)
        {
            _sentShared = true;
            _sentActive = station.Comp.ActiveResearch;
            _sentActiveProgress = activeProgress;
            RaiseNetworkEvent(new FSSharedResearchStateEvent(station.Comp.ActiveResearch, activeProgress), Filter.Broadcast());
        }
    }

    // Field by field: progress moves several times a second, the unlocked list and queue hardly ever.
    private void SyncConsole(Entity<FSTechDatabaseComponent> console, FSStationResearchComponent station,
        Dictionary<string, List<int>> contributorSlots)
    {
        var comp = console.Comp;

        if (!comp.UnlockedNodes.SequenceEqual(station.UnlockedNodes))
        {
            comp.UnlockedNodes.Clear();
            comp.UnlockedNodes.AddRange(station.UnlockedNodes);
            DirtyField(console, comp, nameof(FSTechDatabaseComponent.UnlockedNodes));
        }

        if (comp.ActiveResearch != station.ActiveResearch)
        {
            comp.ActiveResearch = station.ActiveResearch;
            DirtyField(console, comp, nameof(FSTechDatabaseComponent.ActiveResearch));
        }

        if (!comp.SharedQueue.SequenceEqual(station.SharedQueue))
        {
            comp.SharedQueue.Clear();
            comp.SharedQueue.AddRange(station.SharedQueue);
            DirtyField(console, comp, nameof(FSTechDatabaseComponent.SharedQueue));
        }

        if (!SameProgress(comp.NodeProgress, station.NodeProgress))
        {
            comp.NodeProgress.Clear();
            foreach (var (id, amount) in station.NodeProgress)
                comp.NodeProgress[id] = amount;
            DirtyField(console, comp, nameof(FSTechDatabaseComponent.NodeProgress));
        }

        if (comp.Points != station.Points)
        {
            comp.Points = station.Points;
            DirtyField(console, comp, nameof(FSTechDatabaseComponent.Points));
        }

        if (!SameSlots(comp.PersonalContributorSlots, contributorSlots))
        {
            comp.PersonalContributorSlots.Clear();
            foreach (var (id, slots) in contributorSlots)
                comp.PersonalContributorSlots[id] = new List<int>(slots);
            DirtyField(console, comp, nameof(FSTechDatabaseComponent.PersonalContributorSlots));
        }
    }

    private static bool SameProgress(Dictionary<string, int> shown, Dictionary<string, int> actual)
    {
        if (shown.Count != actual.Count)
            return false;

        foreach (var (id, amount) in actual)
        {
            if (!shown.TryGetValue(id, out var have) || have != amount)
                return false;
        }

        return true;
    }

    private static bool SameSlots(Dictionary<string, List<int>> shown, Dictionary<string, List<int>> actual)
    {
        if (shown.Count != actual.Count)
            return false;

        foreach (var (id, slots) in actual)
        {
            if (!shown.TryGetValue(id, out var have) || !have.SequenceEqual(slots))
                return false;
        }

        return true;
    }

    private static int GetOrAssignColorSlot(Entity<FSStationResearchComponent> station, EntityUid mindId)
    {
        if (station.Comp.ContributorColorSlots.TryGetValue(mindId, out var slot))
            return slot;

        slot = station.Comp.ContributorColorSlots.Count;
        station.Comp.ContributorColorSlots[mindId] = slot;
        return slot;
    }

    private bool IsRdOrCaptain(EntityUid player)
    {
        var tags = _accessReader.FindAccessTags(player);
        return tags.Contains(ResearchDirectorAccess) || tags.Contains(CaptainAccess);
    }

    private static bool IsOtherTrack(FSTechDatabaseComponent comp)
        => comp.Track != FSResearchTrack.Science;

    // The console only researches its own branches; a crafted message could otherwise buy medical nodes with RP.
    private static bool ServesBranch(FSTechDatabaseComponent comp, FSTechNodePrototype node)
        => comp.Branches.Count == 0 || comp.Branches.Contains(node.Branch);

    private void OnSelectResearchNode(EntityUid uid, FSTechDatabaseComponent comp, FSSelectResearchNodeMessage args)
    {
        if (comp.Track == FSResearchTrack.Medical)
        {
            _medicalResearch.OnBuyNode(uid, comp, args);
            return;
        }

        if (IsOtherTrack(comp))
            return;

        var player = args.Actor;
        if (!player.IsValid())
            return;

        if (!PrototypeManager.TryIndex<FSTechNodePrototype>(args.NodeId, out var node) || !ServesBranch(comp, node))
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
        if (IsOtherTrack(comp))
            return;

        var player = args.Actor;
        if (!player.IsValid())
            return;

        if (!PrototypeManager.TryIndex<FSTechNodePrototype>(args.NodeId, out var node) || !ServesBranch(comp, node))
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
        if (IsOtherTrack(comp))
            return;

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
        if (IsOtherTrack(comp))
            return;

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
        if (IsOtherTrack(comp))
            return;

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
        while (query.MoveNext(out var consoleUid, out var console))
        {
            if (console.Track != FSResearchTrack.Science)
                continue;

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
            _reinvestPending = true;
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

        if (contributorMindId is { } credited)
        {
            var contributed = new FSResearchContributedEvent(amount);
            RaiseLocalEvent(credited, ref contributed);
        }

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

        if (targetId is not { } id
            || !PrototypeManager.TryIndex<FSTechNodePrototype>(id, out var node)
            || IsExclusivelyBlocked(node, station.Comp.UnlockedLookup))
        {
            station.Comp.Points += amount;
            Dirty(station);
            _progressSyncPending = true;
            return;
        }

        var discounted = id.Id == station.Comp.ActiveResearch?.Id;
        var multiplier = discounted ? 2 : 1;

        var current = station.Comp.NodeProgress.GetValueOrDefault(id.Id);
        var room = Math.Max(0, node.Cost - current);
        var maxUsableRp = (room + multiplier - 1) / multiplier;
        var applied = Math.Min(amount, maxUsableRp);
        var progressGain = applied * multiplier;
        var overflow = amount - applied;

        station.Comp.NodeProgress[id.Id] = current + progressGain;
        if (overflow > 0)
            station.Comp.Points += overflow;

        Dirty(station);

        if (station.Comp.NodeProgress[id.Id] >= node.Cost)
        {
            CompleteResearch(station, node, discounted, contributorMindId);
            SyncConsoles();
            if (!discounted && contributorMindId is { } finisher)
                SendPersonalResearchState(finisher);
            return;
        }

        _progressSyncPending = true;
        if (!discounted && contributorMindId is { } cid)
            _personalSyncPending.Add(cid);
    }

    private void CompleteResearch(Entity<FSStationResearchComponent> station, FSTechNodePrototype node, bool wasShared, EntityUid? contributorMindId)
    {
        MarkNodeUnlocked(station.Comp, node.ID);

        // Picks that this unlock now rules out (the other half of an exclusive choice) are dropped with it,
        // otherwise both halves kept earning and could both unlock.
        if (wasShared || IsBlockedBy(station.Comp.ActiveResearch, station.Comp))
        {
            station.Comp.ActiveResearch = null;
            station.Comp.ActiveResearchSetBy = null;
        }

        var stale = station.Comp.PersonalPicks
            .Where(kv => kv.Value.Id == node.ID || IsBlockedBy(kv.Value, station.Comp))
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

        var completed = new FSResearchNodeCompletedEvent(node.ID);

        RaiseLocalEvent(ref completed);

        Log.Info($"Completed node {node.ID}");
    }

    private bool IsBlockedBy(ProtoId<FSTechNodePrototype>? pick, FSStationResearchComponent station)
    {
        return pick is { } id
               && PrototypeManager.TryIndex(id, out var picked)
               && IsExclusivelyBlocked(picked, station.UnlockedLookup);
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

    public void BroadcastUnlockedNodes()
    {
        if (!TryGetStation(out var station))
            return;

        var unlocked = station.Comp.UnlockedNodes.Select(n => n.Id).ToHashSet();
        unlocked.UnionWith(_medicalResearch.UnlockedNodeIds());
        RaiseNetworkEvent(new FSResearchUnlocksChangedEvent(unlocked), Filter.Broadcast());
    }

    public bool IsNodeUnlocked(string nodeId)
    {
        if (!TryGetStation(out var station))
            return false;

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
            var completed = new FSResearchNodeCompletedEvent(node.ID, Earned: false);
            RaiseLocalEvent(ref completed);
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

    public int ResetAllNodes()
    {
        var station = GetOrCreateStation();
        var count = station.Comp.UnlockedNodes.Count;
        var pickers = station.Comp.PersonalPicks.Keys.ToList();

        ClearUnlockedNodes(station.Comp);
        station.Comp.NodeProgress.Clear();
        station.Comp.Points = 0;
        station.Comp.ActiveResearch = null;
        station.Comp.ActiveResearchSetBy = null;
        station.Comp.SharedQueue.Clear();
        station.Comp.PersonalPicks.Clear();
        station.Comp.PersonalQueues.Clear();
        station.Comp.ContributorColorSlots.Clear();

        Dirty(station);
        SyncConsoles();
        BroadcastUnlockedNodes();

        foreach (var mind in pickers)
            SendPersonalResearchState(mind);

        return count;
    }
}
