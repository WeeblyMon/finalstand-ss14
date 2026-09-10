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
        SubscribeLocalEvent<FSMedicalFundBalanceChangedEvent>(OnFundChanged);
    }

    private void OnFundChanged(ref FSMedicalFundBalanceChangedEvent args)
    {
        SyncConsoles();
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

    public void OnBuyNode(EntityUid uid, FSTechDatabaseComponent console, FSSelectResearchNodeMessage args)
    {
        if (console.Track != FSResearchTrack.Medical)
            return;

        var player = args.Actor;
        if (!player.IsValid() || !PrototypeManager.TryIndex<FSTechNodePrototype>(args.NodeId, out var node))
            return;

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

        if (!TryPurchase(node.ID))
        {
            _popup.PopupEntity(Loc.GetString("fs-medical-research-insufficient-funds", ("cost", node.Cost)), uid, player);
            return;
        }

        _popup.PopupEntity(Loc.GetString("fs-medical-research-purchased", ("name", node.Name)), uid, player);
        Log.Info($"[FSMedResearch] {ToPrettyString(player)} bought {node.ID} for {node.Cost}");
    }

    public bool TryPurchase(string nodeId)
    {
        if (!PrototypeManager.TryIndex<FSTechNodePrototype>(nodeId, out var node))
            return false;

        if (IsNodeUnlocked(node.ID))
            return false;

        if (!ArePrerequisitesMet(node, IsNodeUnlocked))
            return false;

        var state = GetOrCreateState();
        if (IsExclusivelyBlocked(node, state.Comp.UnlockedNodes.Select(n => n.Id).ToList()))
            return false;

        if (!_fund.TryDeductMedicalFunds(node.Cost))
            return false;

        state.Comp.UnlockedNodes.Add(node.ID);
        state.Comp.UnlockedLookup.Add(node.ID);
        SyncConsoles();

        RaiseLocalEvent(new FSResearchNodeCompletedEvent(node.ID, earned: false));
        return true;
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

            console.Points = balance;

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
