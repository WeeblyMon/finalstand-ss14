// Engineering research nodes: answers "is it unlocked" for the systems that check inline, and pushes the rest onto HMGs, crates and rivet guns, built or not.
using Content.Server._FinalStand.Research;
using Content.Shared._FinalStand.HMG;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Weapons.Ranged.Systems;

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
    [Dependency] private SharedGunSystem _gun = default!;

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

    private void OnNodeCompleted(FSResearchNodeCompletedEvent ev)
    {
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

    private void ApplyHmg(Entity<FSHMGComponent> ent)
    {
        var arc = Has(FSEngineeringNodes.TraverseGearing) ? TraverseArcBonus : 0f;
        var heat = Has(FSEngineeringNodes.CoolingJacket) ? CoolingHeatMultiplier : 1f;
        ent.Comp.LinkBonus = Has(FSEngineeringNodes.BeltFeed) ? BeltFeedRangeBonus : 0f;
        if (ent.Comp.ArcBonus.Equals(arc) && ent.Comp.HeatMultiplier.Equals(heat))
            return;

        ent.Comp.ArcBonus = arc;
        ent.Comp.HeatMultiplier = heat;
        Dirty(ent);
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
        var rate = Has(FSEngineeringNodes.PneumaticRivets) ? PneumaticFireRate : 1f;
        var cap = Has(FSEngineeringNodes.PrecisionRivets) ? PrecisionCap : BaseRivetCap;
        if (ent.Comp.FireRateMultiplier.Equals(rate) && ent.Comp.RepairCap.Equals(cap))
            return;

        ent.Comp.RepairCap = cap;
        if (!ent.Comp.FireRateMultiplier.Equals(rate))
        {
            ent.Comp.FireRateMultiplier = rate;
            _gun.RefreshModifiers(ent.Owner);
        }

        Dirty(ent);
    }
}
