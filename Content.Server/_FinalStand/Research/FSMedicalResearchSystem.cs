using System.Linq;
using Content.Server._FinalStand.MedicalOps;
using Content.Server.Popups;
using Content.Shared._FinalStand.Research;
using Content.Shared._FinalStand.Research.Components;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared._FinalStand.Research.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Research.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Research;

// The CMO's tree. Nodes are bought outright with department funds rather than accumulated, so this
// shares the console component and the UI with science but none of its research-point machinery.
public sealed partial class FSMedicalResearchSystem : SharedFSResearchSystem
{
    [Dependency] private FSMedicalFundSystem _fund = default!;
    [Dependency] private PopupSystem _popup = default!;

    private static readonly ProtoId<FSTechBranchPrototype> MedicalBranch = "Medical";

    private EntityUid? _state;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<FSMedicalResearchComponent, EntityTerminatingEvent>(OnStateTerminating);

        // FSResearchSystem subscribes the same message on the same component and key; both handlers
        // run and each ignores the other track.
        Subs.BuiEvents<FSTechDatabaseComponent>(ResearchConsoleUiKey.Key, subs =>
        {
            subs.Event<FSSelectResearchNodeMessage>(OnBuyNode);
        });
    }

    public Entity<FSMedicalResearchComponent> GetOrCreateState()
    {
        if (_state is { } cached && Exists(cached) && TryComp<FSMedicalResearchComponent>(cached, out var cachedComp))
            return (cached, cachedComp);

        var query = EntityQueryEnumerator<FSMedicalResearchComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            _state = uid;
            return (uid, comp);
        }

        var spawned = Spawn(null, MapCoordinates.Nullspace);
        var spawnedComp = AddComp<FSMedicalResearchComponent>(spawned);
        _state = spawned;
        return (spawned, spawnedComp);
    }

    private void OnStateTerminating(EntityUid uid, FSMedicalResearchComponent comp, ref EntityTerminatingEvent args)
    {
        if (_state == uid)
            _state = null;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _state = null;

        var query = EntityQueryEnumerator<FSMedicalResearchComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            comp.UnlockedNodes.Clear();
            comp.UnlockedLookup.Clear();
        }

        SyncConsoles();
    }

    public bool IsNodeUnlocked(string nodeId)
    {
        var state = GetOrCreateState();
        SyncLookup(state.Comp);
        return state.Comp.UnlockedLookup.Contains(nodeId);
    }

    private void OnBuyNode(EntityUid uid, FSTechDatabaseComponent console, FSSelectResearchNodeMessage args)
    {
        if (console.Track != FSResearchTrack.Medical)
            return;

        var player = args.Actor;
        if (!player.IsValid() || !PrototypeManager.TryIndex<FSTechNodePrototype>(args.NodeId, out var node))
            return;

        // A console can only ever buy from the branches it displays.
        if (node.Branch != MedicalBranch)
            return;

        if (IsNodeUnlocked(node.ID))
        {
            _popup.PopupEntity(Loc.GetString("fs-research-already-unlocked"), uid, player);
            return;
        }

        if (!_fund.IsCmo(player))
        {
            RaiseNetworkEvent(new FSResearchAuthorityDeniedEvent(Loc.GetString("fs-medical-research-no-authority")),
                Filter.Entities(player));
            return;
        }

        if (!ArePrerequisitesMet(node, IsNodeUnlocked))
        {
            _popup.PopupEntity(Loc.GetString("fs-research-prereqs-not-met"), uid, player);
            return;
        }

        var state = GetOrCreateState();
        var unlockedIds = state.Comp.UnlockedNodes.Select(n => n.Id).ToList();
        if (IsExclusivelyBlocked(node, unlockedIds))
        {
            _popup.PopupEntity(Loc.GetString("fs-research-exclusive-locked"), uid, player);
            return;
        }

        if (!_fund.TryDeductMedicalFunds(node.Cost))
        {
            _popup.PopupEntity(Loc.GetString("fs-medical-research-insufficient-funds", ("cost", node.Cost)), uid, player);
            return;
        }

        state.Comp.UnlockedNodes.Add(node.ID);
        state.Comp.UnlockedLookup.Add(node.ID);
        SyncConsoles();

        // Same event science raises, so vanilla technology unlocks keep working unchanged.
        RaiseLocalEvent(new FSResearchNodeCompletedEvent(node.ID));

        _popup.PopupEntity(Loc.GetString("fs-medical-research-purchased", ("name", node.Name)), uid, player);
        Log.Info($"[FSMedResearch] {ToPrettyString(player)} bought {node.ID} for {node.Cost}");
    }

    public void SyncConsoles()
    {
        var state = GetOrCreateState();
        var balance = _fund.GetBalance();

        var query = EntityQueryEnumerator<FSTechDatabaseComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (console.Track != FSResearchTrack.Medical)
                continue;

            console.UnlockedNodes.Clear();
            console.UnlockedNodes.AddRange(state.Comp.UnlockedNodes);

            // The header label renders this; on a medical console it reads as the department balance
            // rather than research points.
            console.Points = balance;

            // Deliberately left empty - these drive the progress bar, contributor rings and queue
            // badges, none of which apply to an outright purchase.
            console.NodeProgress.Clear();
            console.SharedQueue.Clear();
            console.PersonalContributorSlots.Clear();
            console.ActiveResearch = null;

            Dirty(uid, console);
        }
    }

    private static void SyncLookup(FSMedicalResearchComponent comp)
    {
        if (comp.UnlockedLookup.Count == comp.UnlockedNodes.Count)
            return;

        comp.UnlockedLookup.Clear();
        foreach (var node in comp.UnlockedNodes)
            comp.UnlockedLookup.Add(node.Id);
    }
}
