using Content.Server._FinalStand.Departments;
using Content.Server._FinalStand.Engineering;
using Content.Server._FinalStand.Materials;
using Content.Server.Mech.Systems;
using Content.Server._FinalStand.Effects;
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
using Content.Shared._FinalStand.Particles;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Mech;

public sealed partial class FSMechBaySystem : EntitySystem
{
    [Dependency] private FSDepartmentAccessSystem _departments = default!;
    [Dependency] private FSSiloNetworkSystem _silo = default!;
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
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private FSFlairSystem _flair = default!;
    [Dependency] private FSWorkSoundSystem _workSound = default!;

    private static readonly ProtoId<FSParticleEmitterPrototype> BayArc = "FSBayArc";
    private static readonly ProtoId<FSParticleEmitterPrototype> DustRing = "FSDustRing";

    private static readonly ProtoId<ToolQualityPrototype> Welding = "Welding";
    private static readonly TimeSpan UpkeepInterval = TimeSpan.FromSeconds(1);
    private const float WeldFuel = 10f;
    private static readonly VerbCategory CraftCategory = new("fs-mech-bay-craft-category", null);
    private static readonly SoundSpecifier StageSound = new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg");
    private static readonly SoundSpecifier RollOutSound = new SoundPathSpecifier("/Audio/Effects/Vehicle/vehiclestartup.ogg");
    private static readonly SoundSpecifier FitSound = new SoundPathSpecifier("/Audio/Items/toolbox_insert.ogg");
    private static readonly SoundSpecifier[] WeldSounds =
    {
        new SoundPathSpecifier("/Audio/Items/welder.ogg"),
        new SoundPathSpecifier("/Audio/Items/welder2.ogg"),
    };
    private static readonly SoundSpecifier[] CraftSounds =
    {
        new SoundPathSpecifier("/Audio/Items/drill_use.ogg"),
        new SoundPathSpecifier("/Audio/Items/ratchet.ogg"),
        new SoundPathSpecifier("/Audio/Items/screwdriver.ogg"),
    };

    private readonly HashSet<Entity<MechComponent>> _parked = new();
    private TimeSpan _nextUpkeep;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMechBayComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<FSMechBayComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<FSMechBayComponent, FSMechBayWeldDoAfterEvent>(OnWelded);
        SubscribeLocalEvent<FSMechBayComponent, FSMechBayCraftDoAfterEvent>(OnCrafted);
        SubscribeLocalEvent<FSMechBayComponent, GetVerbsEvent<ExamineVerb>>(OnGetVerbs);
        SubscribeLocalEvent<FSMechBayComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractUsing(Entity<FSMechBayComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Ready)
            return;

        args.Handled = true;
        var used = MetaData(args.Used).EntityPrototype?.ID;
        foreach (var slot in ent.Comp.Slots)
        {
            var plate = slot.Plate?.Id == used;
            if (!plate && !slot.Parts.Exists(p => p.Id == used))
                continue;

            if (!ent.Comp.CanInstall(slot) || plate && ent.Comp.Plated)
            {
                _popup.PopupEntity(Loc.GetString(ent.Comp.Installed.Contains(slot.Id) || plate && ent.Comp.Plated ? "fs-mech-bay-slot-filled" : "fs-mech-bay-slot-not-yet",
                    ("slot", Loc.GetString(slot.Name))), ent, args.User);
                return;
            }

            QueueDel(args.Used);
            _audio.PlayPvs(FitSound, ent);
            if (!plate)
            {
                Install(ent, slot);
                return;
            }

            ent.Comp.Plated = true;
            Dirty(ent);
            _popup.PopupEntity(Loc.GetString("fs-mech-bay-plate-fitted"), ent, args.User, PopupType.Medium);
            return;
        }

        if (!_tool.HasQuality(args.Used, Welding))
        {
            _popup.PopupEntity(NextStep(ent.Comp), ent, args.User);
            return;
        }

        if (!_departments.IsEngineering(args.User))
        {
            _popup.PopupEntity(Loc.GetString("fs-mech-bay-wrong-role", ("role", RoleName(FSMechBayRole.Engineering))), ent, args.User);
            return;
        }

        if (ent.Comp.Slots.Find(s => s.Weldable && ent.Comp.CanInstall(s)) is not { } weld)
        {
            _popup.PopupEntity(NextStep(ent.Comp), ent, args.User);
            return;
        }

        if (!PlateFitted(ent.Comp, weld) && !_silo.Has(args.User, weld.Materials))
        {
            _popup.PopupEntity(Loc.GetString("fs-mech-bay-needs-materials", ("list", MaterialList(_silo.Missing(args.User, weld.Materials)))), ent, args.User);
            return;
        }

        if (_tool.UseTool(args.Used, args.User, ent, weld.Time, Welding, new FSMechBayWeldDoAfterEvent(weld.Id), WeldFuel))
            _workSound.Start(ent, Transform(ent).Coordinates, WeldSounds, weld.Time);
    }

    private void OnWelded(Entity<FSMechBayComponent> ent, ref FSMechBayWeldDoAfterEvent args)
    {
        _workSound.Stop(ent);
        var id = args.Slot;
        if (args.Cancelled || args.Handled || ent.Comp.Slots.Find(s => s.Id == id) is not { } slot || !ent.Comp.CanInstall(slot))
            return;

        args.Handled = true;
        if (PlateFitted(ent.Comp, slot))
        {
            Install(ent, slot);
            return;
        }

        if (!_silo.TryConsume(args.User, slot.Materials))
        {
            _popup.PopupEntity(Loc.GetString("fs-mech-bay-needs-materials", ("list", MaterialList(_silo.Missing(args.User, slot.Materials)))), ent, args.User);
            return;
        }

        _telemetry.RecordSpend("mech-bay", slot.Materials);
        Install(ent, slot);
    }

    private void Install(Entity<FSMechBayComponent> ent, FSMechBaySlot slot)
    {
        ent.Comp.Installed.Add(slot.Id);
        Dirty(ent);
        _audio.PlayPvs(StageSound, ent);
        _flair.Burst(BayArc, ent);
        if (ent.Comp.Ready)
        {
            RollOut(ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("fs-mech-bay-slot-done", ("slot", Loc.GetString(slot.Name)),
            ("done", ent.Comp.Installed.Count), ("total", ent.Comp.Slots.Count)), ent, PopupType.Medium);
    }

    private void OnActivate(Entity<FSMechBayComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Ready)
            RollOut(ent);
        else
            _popup.PopupEntity(NextStep(ent.Comp), ent, args.User);
    }

    private string NextStep(FSMechBayComponent bay)
    {
        if (bay.Next() is not { } slot)
            return Loc.GetString("fs-mech-bay-examine-ready");

        var name = Loc.GetString(slot.Name);
        var role = RoleName(slot.Role);
        if (PlateFitted(bay, slot))
            return Loc.GetString("fs-mech-bay-next-plate", ("role", role));

        if (!slot.Weldable)
            return Loc.GetString("fs-mech-bay-next-part", ("slot", name), ("part", PartName(slot.Parts[0])), ("role", role));

        var need = MaterialList(slot.Materials);
        if (slot.Plate is { } plate)
            need = Loc.GetString("fs-mech-bay-or-plate", ("need", need), ("plate", PartName(plate)));
        return Loc.GetString("fs-mech-bay-next-weld", ("slot", name), ("role", role), ("need", need));
    }

    private void RollOut(Entity<FSMechBayComponent> ent)
    {
        var mech = Spawn(ent.Comp.Result, Transform(ent).Coordinates);
        if (ent.Comp.Plated && TryComp<MechComponent>(mech, out var mechComp))
        {
            mechComp.MaxIntegrity *= ent.Comp.PlatedIntegrity;
            mechComp.Integrity = mechComp.MaxIntegrity;
            Dirty(mech, mechComp);
        }

        ent.Comp.Installed.Clear();
        ent.Comp.Plated = false;
        Dirty(ent);
        _audio.PlayPvs(RollOutSound, ent);
        _flair.Burst(DustRing, ent, kick: 0.6f);
        _popup.PopupEntity(Loc.GetString("fs-mech-bay-rolled-out"), ent, PopupType.Large);
    }

    private void OnGetVerbs(Entity<FSMechBayComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_departments.IsEngineering(args.User))
            return;

        var user = args.User;
        for (var i = 0; i < ent.Comp.Crafts.Count; i++)
        {
            var index = i;
            var craft = ent.Comp.Crafts[i];
            args.Verbs.Add(new ExamineVerb
            {
                Text = Loc.GetString("fs-mech-bay-craft-verb", ("name", _proto.Index(craft.Proto).Name),
                    ("list", MaterialList(craft.Materials))),
                Category = CraftCategory,
                ShowOnExamineTooltip = false,
                CloseMenu = true,
                Act = () => StartCraft(ent, user, index),
            });
        }
    }

    private void StartCraft(Entity<FSMechBayComponent> ent, EntityUid user, int index)
    {
        var craft = ent.Comp.Crafts[index];
        if (!_silo.Has(user, craft.Materials))
        {
            _popup.PopupEntity(Loc.GetString("fs-mech-bay-needs-materials", ("list", MaterialList(_silo.Missing(user, craft.Materials)))), ent, user);
            return;
        }

        if (_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, craft.Time, new FSMechBayCraftDoAfterEvent(index), ent, ent)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            }))
            _workSound.Start(ent, Transform(ent).Coordinates, CraftSounds, craft.Time);
    }

    private void OnCrafted(Entity<FSMechBayComponent> ent, ref FSMechBayCraftDoAfterEvent args)
    {
        _workSound.Stop(ent);
        if (args.Cancelled || args.Handled || args.Index < 0 || args.Index >= ent.Comp.Crafts.Count)
            return;

        args.Handled = true;
        var craft = ent.Comp.Crafts[args.Index];
        if (!_silo.TryConsume(args.User, craft.Materials))
            return;

        _telemetry.RecordSpend("mech-bay", craft.Materials);

        var item = Spawn(craft.Proto, Transform(ent).Coordinates);
        _hands.TryPickupAnyHand(args.User, item);
    }

    private void OnExamined(Entity<FSMechBayComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Ready)
        {
            args.PushMarkup(Loc.GetString("fs-mech-bay-examine-ready"));
            return;
        }

        args.PushMarkup(Loc.GetString("fs-mech-bay-examine-progress", ("done", ent.Comp.Installed.Count), ("total", ent.Comp.Slots.Count)));
        if (ent.Comp.Next() is not { } slot)
            return;

        var need = slot.Weldable ? MaterialList(slot.Materials) : PartName(slot.Parts[0]);
        if (PlateFitted(ent.Comp, slot))
            need = Loc.GetString("fs-mech-bay-fitted-plate");
        else if (slot.Plate is { } plate)
            need = Loc.GetString("fs-mech-bay-or-plate", ("need", need), ("plate", PartName(plate)));
        args.PushMarkup(Loc.GetString("fs-mech-bay-examine-next", ("slot", Loc.GetString(slot.Name)),
            ("role", RoleName(slot.Role)), ("need", need), ("weld", slot.Weldable ? "yes" : "no")));
    }

    private static bool PlateFitted(FSMechBayComponent bay, FSMechBaySlot slot) => bay.Plated && slot.Plate != null;

    private string RoleName(FSMechBayRole role) => Loc.GetString(role == FSMechBayRole.Engineering ? "fs-mech-bay-role-engineering" : "fs-mech-bay-role-science");

    private string PartName(EntProtoId part) => _proto.Index(part).Name;

    private string MaterialList(Dictionary<ProtoId<StackPrototype>, int> materials) => FSMaterialText.List(_proto, materials);

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
