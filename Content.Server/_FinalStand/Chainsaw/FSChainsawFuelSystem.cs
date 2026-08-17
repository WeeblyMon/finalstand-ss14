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
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private const string FuelReagent = "WeldingFuel";
    private const string WelderSolution = "Welder";
    private const string TankSolution = "tank";

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
        if (comp.CurrentFuel >= FuelPerSwing(uid, comp))
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString("fs-chainsaw-no-fuel");
    }

    private void OnMeleeHit(EntityUid uid, FSChainsawFuelComponent comp, MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        comp.CurrentFuel = MathF.Max(0f, comp.CurrentFuel - FuelPerSwing(uid, comp));
        Dirty(uid, comp);

        if (comp.CurrentFuel <= 0f)
            _popup.PopupEntity(Loc.GetString("fs-chainsaw-sputters"), uid, args.User);
    }

    private void OnExamined(EntityUid uid, FSChainsawFuelComponent comp, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("fs-chainsaw-examine-fuel",
            ("fuel", (int) comp.CurrentFuel),
            ("max", (int) comp.MaxFuel)));
    }

    private void OnInteractUsing(EntityUid uid, FSChainsawFuelComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryRefuelFrom(uid, comp, args.Used, args.User);
    }

    private void OnAfterInteract(EntityUid uid, FSChainsawFuelComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = TryRefuelFrom(uid, comp, target, args.User);
    }

    private bool TryRefuelFrom(EntityUid chainsaw, FSChainsawFuelComponent comp, EntityUid source, EntityUid user)
    {
        if (!_solution.TryGetDrainableSolution(source, out var sourceSolEnt, out var sourceSol)
            && !_solution.TryGetSolution(source, WelderSolution, out sourceSolEnt, out sourceSol)
            && !_solution.TryGetSolution(source, TankSolution, out sourceSolEnt, out sourceSol))
            return false;

        var available = sourceSol.GetTotalPrototypeQuantity(FuelReagent);
        if (available <= FixedPoint2.Zero)
            return false;

        var space = comp.MaxFuel - comp.CurrentFuel;
        if (space <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("fs-chainsaw-tank-full"), chainsaw, user);
            return true;
        }

        var perUnit = MathF.Max(0.01f, comp.FuelPerWelderUnit);
        var wanted = FixedPoint2.New(MathF.Min(available.Float(), space / perUnit));
        var drained = _solution.RemoveReagent(sourceSolEnt.Value, FuelReagent, wanted);

        if (drained <= FixedPoint2.Zero)
            return false;

        comp.CurrentFuel = MathF.Min(comp.MaxFuel, comp.CurrentFuel + drained.Float() * perUnit);
        Dirty(chainsaw, comp);

        _popup.PopupEntity(Loc.GetString("fs-chainsaw-refuel", ("amount", drained.Int())), chainsaw, user);
        return true;
    }

    private float FuelPerSwing(EntityUid uid, FSChainsawFuelComponent comp)
    {
        var reduction = TryComp<FSWeaponUpgradeStateComponent>(uid, out var state)
            ? state.FuelEfficiencyReduction
            : 0f;

        return MathF.Max(0.1f, comp.BaseFuelPerSwing - reduction);
    }
}
