// Chainsaw fuel mechanic: deplete per swing, refill from any WeldingFuel container (welder, tank, wall dispenser).
using Content.Shared._FinalStand.Chainsaw;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._FinalStand.Chainsaw;

public sealed class FSChainsawFuelSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSChainsawFuelComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<FSChainsawFuelComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<FSChainsawFuelComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FSChainsawFuelComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<FSChainsawFuelComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAttemptMelee(EntityUid uid, FSChainsawFuelComponent comp, ref AttemptMeleeEvent args)
    {
        if (comp.CurrentFuel >= EffectiveFuelPerSwing(uid, comp))
            return;
        args.Cancelled = true;
        args.Message = "Out of fuel.";
    }

    private void OnMeleeHit(EntityUid uid, FSChainsawFuelComponent comp, MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;
        var cost = EffectiveFuelPerSwing(uid, comp);
        comp.CurrentFuel = MathF.Max(0f, comp.CurrentFuel - cost);
        Dirty(uid, comp);

        if (comp.CurrentFuel <= 0f)
            _popup.PopupEntity("The chainsaw sputters and dies.", uid, args.User);
    }

    private void OnExamined(EntityUid uid, FSChainsawFuelComponent comp, ExaminedEvent args)
    {
        args.PushMarkup($"Fuel: [color=yellow]{(int)comp.CurrentFuel}[/color] / {(int)EffectiveMaxFuel(uid, comp)}");
    }

    private void OnInteractUsing(EntityUid uid, FSChainsawFuelComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;
        if (TryDrainWeldingFuelFrom(uid, comp, args.Used, args.User))
            args.Handled = true;
    }

    private void OnAfterInteract(EntityUid uid, FSChainsawFuelComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;
        if (TryDrainWeldingFuelFrom(uid, comp, target, args.User))
            args.Handled = true;
    }

    private bool TryDrainWeldingFuelFrom(EntityUid chainsaw, FSChainsawFuelComponent comp, EntityUid source, EntityUid user)
    {
        if (!_solution.TryGetDrainableSolution(source, out var sourceSolEnt, out var sourceSol)
            && !_solution.TryGetSolution(source, "Welder", out sourceSolEnt, out sourceSol)
            && !_solution.TryGetSolution(source, "tank", out sourceSolEnt, out sourceSol))
            return false;

        var fuelAvailable = sourceSol.GetTotalPrototypeQuantity("WeldingFuel");
        if (fuelAvailable <= FixedPoint2.Zero)
            return false;

        var maxFuel = EffectiveMaxFuel(chainsaw, comp);
        var space = maxFuel - comp.CurrentFuel;
        if (space <= 0f)
        {
            _popup.PopupEntity("The chainsaw's tank is full.", chainsaw, user);
            return true;
        }

        var maxXferUnits = MathF.Min(fuelAvailable.Float(), space / comp.FuelPerWelderUnit);
        var xferUnits = FixedPoint2.New(maxXferUnits);
        _solution.RemoveReagent(sourceSolEnt.Value, "WeldingFuel", xferUnits);

        comp.CurrentFuel = MathF.Min(maxFuel, comp.CurrentFuel + xferUnits.Float() * comp.FuelPerWelderUnit);
        Dirty(chainsaw, comp);

        _popup.PopupEntity($"You refuel the chainsaw (+{xferUnits.Int()}).", chainsaw, user);
        return true;
    }

    private static float EffectiveFuelPerSwing(EntityUid uid, FSChainsawFuelComponent comp)
    {
        var entSys = IoCManager.Resolve<IEntityManager>();
        var reduction = 0f;
        if (entSys.TryGetComponent<FSWeaponUpgradeStateComponent>(uid, out var state))
            reduction = state.FuelEfficiencyReduction;
        return MathF.Max(0.1f, comp.BaseFuelPerSwing - reduction);
    }

    private static float EffectiveMaxFuel(EntityUid uid, FSChainsawFuelComponent comp)
    {
        return comp.BaseMaxFuel * comp.MaxFuelMultiplier;
    }
}
