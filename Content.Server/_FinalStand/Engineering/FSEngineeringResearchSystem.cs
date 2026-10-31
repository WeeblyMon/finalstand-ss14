using Content.Server._FinalStand.Departments;
using Content.Server._FinalStand.Research;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Content.Shared._FinalStand.HMG;
using Content.Shared._FinalStand.Repair;

namespace Content.Server._FinalStand.Engineering;

public static class FSEngineeringNodes
{
    public const string QuickSetPlating = "FSEngineeringQuickSetPlating";
    public const string RapidAssembly = "FSEngineeringRapidAssembly";
    public const string PneumaticRivets = "FSEngineeringPneumaticRivets";
    public const string CompressedBelts = "FSEngineeringCompressedBelts";
    public const string ReinforcedSandbags = "FSEngineeringReinforcedSandbags";
    public const string ArcWelding = "FSEngineeringArcWelding";
    public const string SalvagePlating = "FSEngineeringSalvagePlating";
    public const string RazorCoil = "FSEngineeringRazorCoil";
    public const string PrecisionRivets = "FSEngineeringPrecisionRivets";
    public const string CoolingJacket = "FSEngineeringCoolingJacket";
    public const string OverbuiltBulwark = "FSEngineeringOverbuiltBulwark";
    public const string TraverseGearing = "FSEngineeringTraverseGearing";
    public const string BeltFeed = "FSEngineeringBeltFeed";
    public const string ThirdHardpoint = "FSEngineeringThirdHardpoint";
}

public sealed partial class FSEngineeringResearchSystem : EntitySystem
{
    [Dependency] private FSResearchSystem _research = default!;
    [Dependency] private FSHMGSystem _hmg = default!;
    [Dependency] private FSRivetGunSystem _rivets = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private FSDepartmentAccessSystem _departments = default!;

    private static readonly ProtoId<FSTechBranchPrototype> EngineeringBranch = "Engineering";

    private const float TraverseArcBonus = 30f;
    private const float CoolingHeatMultiplier = 0.65f;
    private const float BeltFeedRangeBonus = 1f;
    private const float CompressedBeltsBonus = 0.5f;
    private const float PneumaticFireRate = 1.4f;
    private const float BaseRivetCap = 0.75f;
    private const float PrecisionCap = 0.9f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSResearchNodeCompletedEvent>(OnNodeCompleted);
        SubscribeLocalEvent<FSHMGComponent, MapInitEvent>((uid, comp, _) => ApplyHmg((uid, comp)));
        SubscribeLocalEvent<FSRivetGunComponent, MapInitEvent>((uid, comp, _) => ApplyRivetGun((uid, comp)));
    }

    public bool Has(string node) => _research.IsNodeUnlocked(node);

    private void OnNodeCompleted(ref FSResearchNodeCompletedEvent ev)
    {
        if (ev.Earned)
            AnnounceToEngineers(ev.NodeId);

        var hmgs = EntityQueryEnumerator<FSHMGComponent>();
        while (hmgs.MoveNext(out var uid, out var comp))
            ApplyHmg((uid, comp));

        var crates = EntityQueryEnumerator<FSAmmoCrateComponent>();
        while (crates.MoveNext(out var uid, out var comp))
            ApplyCrate((uid, comp));

        var guns = EntityQueryEnumerator<FSRivetGunComponent>();
        while (guns.MoveNext(out var uid, out var comp))
            ApplyRivetGun((uid, comp));
    }

    private void AnnounceToEngineers(string nodeId)
    {
        if (!_proto.TryIndex<FSTechNodePrototype>(nodeId, out var node) || node.Branch != EngineeringBranch)
            return;

        var message = Loc.GetString("fs-engineering-research-done", ("name", node.Name), ("bonus", node.BonusDescription));
        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        foreach (var (uid, session) in _departments.Engineers())
        {
            _chat.ChatMessageToOne(ChatChannel.Server, message, wrapped, default, false, session.Channel);
            _popup.PopupEntity(Loc.GetString("fs-engineering-research-done-popup", ("name", node.Name)), uid, uid, PopupType.Medium);
        }
    }

    private void ApplyHmg(Entity<FSHMGComponent> ent)
    {
        _hmg.SetResearchBonuses(ent,
            Has(FSEngineeringNodes.TraverseGearing) ? TraverseArcBonus : 0f,
            Has(FSEngineeringNodes.CoolingJacket) ? CoolingHeatMultiplier : 1f,
            Has(FSEngineeringNodes.BeltFeed) ? BeltFeedRangeBonus : 0f);
    }

    public void ApplyCrate(Entity<FSAmmoCrateComponent> ent)
    {
        if (ent.Comp.Compressed || !Has(FSEngineeringNodes.CompressedBelts))
            return;

        var bonus = (int) (ent.Comp.MaxRounds * CompressedBeltsBonus);
        ent.Comp.Compressed = true;
        ent.Comp.MaxRounds += bonus;
        ent.Comp.Rounds += bonus;
        Dirty(ent);
    }

    private void ApplyRivetGun(Entity<FSRivetGunComponent> ent)
    {
        _rivets.SetResearchBonuses(ent,
            Has(FSEngineeringNodes.PneumaticRivets) ? PneumaticFireRate : 1f,
            Has(FSEngineeringNodes.PrecisionRivets) ? PrecisionCap : BaseRivetCap);
    }
}
