// Runs the Mech Bay: each assembly stage needs the right department and parts, engineers build mech guns at it,
// and a mech parked on it recharges and gets patched up.
using Content.Server._FinalStand.Departments;
using Content.Server._FinalStand.Engineering;
using Content.Server.Mech.Systems;
using Content.Shared._FinalStand.Materials;
using Content.Shared._FinalStand.Mech;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Mech;

public sealed partial class FSMechBaySystem : EntitySystem
{
    [Dependency] private FSDepartmentAccessSystem _departments = default!;
    [Dependency] private FSCarriedMaterialSystem _materials = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MechSystem _mech = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FSEngineeringTelemetrySystem _telemetry = default!;

    private static readonly ProtoId<ToolQualityPrototype> Welding = "Welding";
    private static readonly TimeSpan UpkeepInterval = TimeSpan.FromSeconds(1);
    private const float WeldFuel = 10f;

    private readonly HashSet<Entity<MechComponent>> _parked = new();
    private TimeSpan _nextUpkeep;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMechBayComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<FSMechBayComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<FSMechBayComponent, FSMechBayStageDoAfterEvent>(OnStageWelded);
        SubscribeLocalEvent<FSMechBayComponent, FSMechBayCraftDoAfterEvent>(OnCrafted);
        SubscribeLocalEvent<FSMechBayComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<FSMechBayComponent, ExaminedEvent>(OnExamined);
    }

    private FSMechBayStage? Current(FSMechBayComponent bay) => bay.Stage < bay.Stages.Count ? bay.Stages[bay.Stage] : null;

    private bool InRole(EntityUid user, FSMechBayRole role)
    {
        return role == FSMechBayRole.Engineering ? _departments.IsEngineering(user) : _departments.IsScience(user);
    }

    private void OnInteractUsing(Entity<FSMechBayComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || Current(ent.Comp) is not { } stage)
            return;

        args.Handled = true;
        if (!InRole(args.User, stage.Role))
        {
            _popup.PopupEntity(Loc.GetString("fs-mech-bay-wrong-role", ("role", RoleName(stage.Role))), ent, args.User);
            return;
        }

        if (stage.Part is { } part)
        {
            if (MetaData(args.Used).EntityPrototype?.ID != part.Id)
            {
                _popup.PopupEntity(Loc.GetString("fs-mech-bay-needs-part", ("part", PartName(part))), ent, args.User);
                return;
            }

            QueueDel(args.Used);
            Advance(ent, args.User);
            return;
        }

        if (!_tool.HasQuality(args.Used, Welding))
        {
            _popup.PopupEntity(Loc.GetString("fs-mech-bay-needs-weld"), ent, args.User);
            return;
        }

        if (!_materials.Has(args.User, stage.Materials))
        {
            _popup.PopupEntity(Loc.GetString("fs-mech-bay-needs-materials", ("list", MaterialList(stage.Materials))), ent, args.User);
            return;
        }

        _tool.UseTool(args.Used, args.User, ent, stage.Time, Welding, new FSMechBayStageDoAfterEvent(), WeldFuel);
    }

    private void OnStageWelded(Entity<FSMechBayComponent> ent, ref FSMechBayStageDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || Current(ent.Comp) is not { } stage)
            return;

        args.Handled = true;
        if (!_materials.TryConsume(args.User, stage.Materials))
        {
            _popup.PopupEntity(Loc.GetString("fs-mech-bay-needs-materials", ("list", MaterialList(stage.Materials))), ent, args.User);
            return;
        }

        _telemetry.RecordSpend("mech-bay", stage.Materials);

        Advance(ent, args.User);
    }

    private void Advance(Entity<FSMechBayComponent> ent, EntityUid user)
    {
        ent.Comp.Stage++;
        Dirty(ent);
        var message = Current(ent.Comp) is { } next
            ? Loc.GetString("fs-mech-bay-stage-done", ("next", Loc.GetString(next.Name)), ("role", RoleName(next.Role)))
            : Loc.GetString("fs-mech-bay-ready");
        _popup.PopupEntity(message, ent, user, PopupType.Medium);
    }

    private void OnActivate(Entity<FSMechBayComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || ent.Comp.Stage < ent.Comp.Stages.Count)
            return;

        args.Handled = true;
        ent.Comp.Stage = 0;
        Dirty(ent);
        Spawn(ent.Comp.Result, Transform(ent).Coordinates);
        _popup.PopupEntity(Loc.GetString("fs-mech-bay-rolled-out"), ent, args.User, PopupType.Large);
    }

    private void OnGetVerbs(Entity<FSMechBayComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_departments.IsEngineering(args.User))
            return;

        var user = args.User;
        for (var i = 0; i < ent.Comp.Crafts.Count; i++)
        {
            var index = i;
            var craft = ent.Comp.Crafts[i];
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("fs-mech-bay-craft-verb", ("name", _proto.Index(craft.Proto).Name),
                    ("list", MaterialList(craft.Materials))),
                Act = () => StartCraft(ent, user, index),
            });
        }
    }

    private void StartCraft(Entity<FSMechBayComponent> ent, EntityUid user, int index)
    {
        var craft = ent.Comp.Crafts[index];
        if (!_materials.Has(user, craft.Materials))
        {
            _popup.PopupEntity(Loc.GetString("fs-mech-bay-needs-materials", ("list", MaterialList(craft.Materials))), ent, user);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, craft.Time, new FSMechBayCraftDoAfterEvent(index), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnCrafted(Entity<FSMechBayComponent> ent, ref FSMechBayCraftDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Index < 0 || args.Index >= ent.Comp.Crafts.Count)
            return;

        args.Handled = true;
        var craft = ent.Comp.Crafts[args.Index];
        if (!_materials.TryConsume(args.User, craft.Materials))
            return;

        _telemetry.RecordSpend("mech-bay", craft.Materials);

        var item = Spawn(craft.Proto, Transform(ent).Coordinates);
        _hands.TryPickupAnyHand(args.User, item);
    }

    private void OnExamined(Entity<FSMechBayComponent> ent, ref ExaminedEvent args)
    {
        if (Current(ent.Comp) is not { } stage)
        {
            args.PushMarkup(Loc.GetString("fs-mech-bay-examine-ready"));
            return;
        }

        var need = stage.Part is { } part ? PartName(part) : MaterialList(stage.Materials);
        args.PushMarkup(Loc.GetString("fs-mech-bay-examine-stage",
            ("stage", ent.Comp.Stage + 1), ("total", ent.Comp.Stages.Count),
            ("name", Loc.GetString(stage.Name)), ("role", RoleName(stage.Role)), ("need", need),
            ("weld", stage.Part == null)));
    }

    private string RoleName(FSMechBayRole role) => Loc.GetString(role == FSMechBayRole.Engineering ? "fs-mech-bay-role-engineering" : "fs-mech-bay-role-science");

    private string PartName(EntProtoId part) => _proto.Index(part).Name;

    private string MaterialList(Dictionary<ProtoId<StackPrototype>, int> materials)
    {
        var parts = new List<string>();
        foreach (var (material, amount) in materials)
            parts.Add($"{amount} {Loc.GetString(_proto.Index(material).Name)}");

        return string.Join(", ", parts);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpkeep)
            return;

        _nextUpkeep = _timing.CurTime + UpkeepInterval;
        var seconds = (float) UpkeepInterval.TotalSeconds;

        var bays = EntityQueryEnumerator<FSMechBayComponent, TransformComponent>();
        while (bays.MoveNext(out _, out var bay, out var xform))
        {
            _parked.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, bay.ParkRange, _parked);
            foreach (var mech in _parked)
            {
                if (mech.Comp.Energy < mech.Comp.MaxEnergy)
                    _mech.TryChangeEnergy(mech, FixedPoint2.Min(bay.RechargePerSecond * seconds, mech.Comp.MaxEnergy - mech.Comp.Energy), mech.Comp);

                if (TryComp<DamageableComponent>(mech, out var damageable) && damageable.TotalDamage > 0)
                    _damageable.HealEvenly((mech.Owner, damageable), -FixedPoint2.New(bay.RepairPerSecond * seconds));
            }
        }
    }
}
