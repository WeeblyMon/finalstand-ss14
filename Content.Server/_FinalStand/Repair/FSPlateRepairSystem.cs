using Content.Server._FinalStand.Departments;
using Content.Server._FinalStand.Engineering;
using Content.Server.Destructible;
using Content.Server.Popups;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Repair;

public sealed partial class FSPlateRepairSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private FSDepartmentAccessSystem _departments = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private FSEngineeringResearchSystem _research = default!;

    private static readonly ProtoId<ToolQualityPrototype> Welding = "Welding";
    private const float FitDelay = 1f;
    private const float UntrainedMultiplier = 2f;
    private const float WeldDelay = 2f;
    private const float WeldFuel = 5f;
    private const float QuickSetMultiplier = 0.6f;
    private const float ArcWeldingMultiplier = 0.7f;
    private const float MinWeldSeconds = 1f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSPlateRepairComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<FSPlateRepairComponent, FSPlateFitDoAfterEvent>(OnFitted);
        SubscribeLocalEvent<FSPlateRepairComponent, FSPlateWeldDoAfterEvent>(OnWelded);
        SubscribeLocalEvent<FSPlateRepairComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractUsing(Entity<FSPlateRepairComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || PlatesNeeded(ent) <= 0 && ent.Comp.Fitted == 0)
            return;

        var delayScale = (_departments.IsEngineering(args.User) ? 1f : UntrainedMultiplier) * ent.Comp.DelayMultiplier;

        if (TryComp<StackComponent>(args.Used, out var stack) && stack.StackTypeId == ent.Comp.Material)
        {
            args.Handled = true;
            if (PlatesNeeded(ent) <= 0)
            {
                _popup.PopupEntity(Loc.GetString("fs-plate-repair-weld-now"), ent, args.User);
                return;
            }

            var fit = FitDelay * delayScale * (_research.Has(FSEngineeringNodes.QuickSetPlating) ? QuickSetMultiplier : 1f);
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, fit,
                new FSPlateFitDoAfterEvent(), ent, ent, args.Used)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            });
            return;
        }

        if (ent.Comp.Fitted > 0 && _tool.HasQuality(args.Used, Welding))
            args.Handled = _tool.UseTool(args.Used, args.User, ent, WeldTime(args.Used, delayScale),
                Welding, new FSPlateWeldDoAfterEvent(), WeldFuel);
    }

    private float WeldTime(EntityUid tool, float delayScale)
    {
        var weld = WeldDelay * delayScale;
        if (!_research.Has(FSEngineeringNodes.ArcWelding))
            return weld;

        var toolSpeed = CompOrNull<ToolComponent>(tool)?.SpeedModifier ?? 1f;
        return MathF.Max(weld * ArcWeldingMultiplier, MinWeldSeconds * toolSpeed);
    }

    private void OnFitted(Entity<FSPlateRepairComponent> ent, ref FSPlateFitDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used || PlatesNeeded(ent) <= 0)
            return;

        if (!_stack.TryUse(used, 1))
            return;

        args.Handled = true;
        ent.Comp.Fitted++;
        var remaining = PlatesNeeded(ent);
        _popup.PopupEntity(remaining > 0
            ? Loc.GetString("fs-plate-repair-fitted-more", ("count", remaining), ("material", MaterialName(ent)))
            : Loc.GetString("fs-plate-repair-weld-now"), ent, args.User);
    }

    private void OnWelded(Entity<FSPlateRepairComponent> ent, ref FSPlateWeldDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Fitted == 0)
            return;

        args.Handled = true;
        _damageable.HealEvenly(ent.Owner, -FixedPoint2.New(PerPlate(ent) * ent.Comp.Fitted), origin: args.User);
        ent.Comp.Fitted = 0;
        _popup.PopupEntity(Loc.GetString("fs-plate-repair-welded"), ent, args.User);
    }

    private void OnExamined(Entity<FSPlateRepairComponent> ent, ref ExaminedEvent args)
    {
        var needed = PlatesNeeded(ent);
        if (needed > 0)
            args.PushMarkup(Loc.GetString("fs-plate-repair-examine-needs", ("count", needed), ("material", MaterialName(ent))));
        else if (ent.Comp.Fitted > 0)
            args.PushMarkup(Loc.GetString("fs-plate-repair-examine-weld", ("count", ent.Comp.Fitted)));
    }

    private int PlatesNeeded(Entity<FSPlateRepairComponent> ent)
    {
        var perPlate = PerPlate(ent);
        if (perPlate <= 0f || !TryComp<DamageableComponent>(ent, out var damageable))
            return 0;

        return Math.Max(0, (int) MathF.Ceiling(damageable.TotalDamage.Float() / perPlate) - ent.Comp.Fitted);
    }

    private float PerPlate(Entity<FSPlateRepairComponent> ent)
    {
        return _destructible.TryGetDestroyedAt(ent.Owner, out var max) && max > 0
            ? max.Value.Float() / ent.Comp.Plates
            : 0f;
    }

    private string MaterialName(Entity<FSPlateRepairComponent> ent)
    {
        return Loc.GetString(_proto.Index(ent.Comp.Material).Name);
    }
}
