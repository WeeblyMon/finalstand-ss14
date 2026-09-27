using System.Linq;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Utility;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Labels.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Storage;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSFieldKitStationSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private OpenableSystem _openable = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private FSTreatmentAttributionSystem _attribution = default!;

    private static readonly SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSFieldKitStationComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSFieldKitStationComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<FSFieldKitStationComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<FSFieldKitStationComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<FSFieldKitStationComponent, InteractUsingEvent>(OnInteractUsing, before: [typeof(ItemSlotsSystem)]);

        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitDumpSatchelMessage>(OnDump);
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitBrewMessage>(OnBrew);
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitDispenseMessage>(OnDispense);
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitDiscardMessage>(OnDiscard);
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitEmptyJugMessage>(OnEmptyJug);
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitEjectMessage>(OnEject);
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitLabelMessage>(OnLabel);
        InitializePackaging();
    }

    private void OnStartup(Entity<FSFieldKitStationComponent> ent, ref ComponentStartup args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.RackContainer);
    }

    private void OnContainerChanged<T>(Entity<FSFieldKitStationComponent> ent, ref T args) => UpdateUi(ent);

    private void OnUiOpened(Entity<FSFieldKitStationComponent> ent, ref BoundUIOpenedEvent args) => UpdateUi(ent);

    // Satchels empty into the tank and empty flasks go on the rack, before the jug slot or storage can claim them.
    private void OnInteractUsing(Entity<FSFieldKitStationComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<FSHarvestSatchelComponent>(args.Used))
        {
            args.Handled = true;
            DumpSatchel(ent, args.Used, args.User);
            return;
        }

        if (!TryComp<FSSplashFlaskComponent>(args.Used, out var flask)
            || !_solutions.TryGetSolution(args.Used, flask.Solution, out _, out var contents))
            return;

        args.Handled = true;
        var rack = _container.EnsureContainer<Container>(ent, ent.Comp.RackContainer);
        if (contents.Volume > 0)
            _popup.PopupEntity(Loc.GetString("fs-field-kit-rack-not-empty"), ent, args.User);
        else if (rack.ContainedEntities.Count >= ent.Comp.RackCapacity)
            _popup.PopupEntity(Loc.GetString("fs-field-kit-rack-full"), ent, args.User);
        else
            _container.Insert(args.Used, rack);
    }

    private void OnDump(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitDumpSatchelMessage args)
    {
        var moved = FixedPoint2.Zero;
        foreach (var item in _inventory.GetHandOrInventoryEntities(args.Actor))
        {
            if (HasComp<FSHarvestSatchelComponent>(item))
                moved += DumpSatchel(ent, item, args.Actor, popup: false);
        }

        _popup.PopupEntity(moved > 0
            ? Loc.GetString("fs-field-kit-dumped", ("amount", moved.Int()))
            : Loc.GetString("fs-field-kit-no-satchel"), ent, args.Actor);
        UpdateUi(ent);
    }

    private FixedPoint2 DumpSatchel(Entity<FSFieldKitStationComponent> ent, EntityUid satchel, EntityUid user, bool popup = true)
    {
        if (!TryComp<FSHarvestSatchelComponent>(satchel, out var comp)
            || !_solutions.TryGetSolution(satchel, comp.Solution, out var from, out _)
            || !TryGetTank(ent, out var tank, out var tankSolution))
            return FixedPoint2.Zero;

        var room = tankSolution.AvailableVolume;
        var moved = _solutions.RemoveReagent(from.Value, comp.Reagent, room);
        if (moved > 0)
            _solutions.TryAddReagent(tank, comp.Reagent, moved);

        if (popup)
        {
            _popup.PopupEntity(moved > 0
                ? Loc.GetString("fs-field-kit-dumped", ("amount", moved.Int()))
                : Loc.GetString(room <= 0 ? "fs-field-kit-tank-full" : "fs-field-kit-satchel-empty"), ent, user);
            UpdateUi(ent);
        }

        return moved;
    }

    // Runs the kit's reaction at the station and puts only the products in the jug, so a full jug brews its whole volume.
    private void OnBrew(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitBrewMessage args)
    {
        if (!Ready(ent, args.Actor, out var jugItem, out var jug, out var jugSolution)
            || !_prototypes.TryIndex(args.Kit, out var kit)
            || kit.Reaction is not { } reactionId
            || !_prototypes.TryIndex(reactionId, out ReactionPrototype? reaction)
            || !TryGetTank(ent, out var tank, out var tankSolution))
            return;

        var yield = reaction.Products.Values.Aggregate(FixedPoint2.Zero, (sum, q) => sum + q);
        if (yield <= 0)
            return;

        var batches = (int) Math.Floor(Math.Min(args.Volume, jugSolution.AvailableVolume.Float()) / yield.Float());
        if (batches < 1)
        {
            Popup(ent, args.Actor, "fs-field-kit-jug-full");
            return;
        }

        foreach (var (reagent, reactant) in reaction.Reactants)
        {
            var need = reactant.Amount * batches;
            var have = reagent == ent.Comp.TissueReagent.Id ? tankSolution.GetTotalPrototypeQuantity(reagent) : StoredQuantity(ent, reagent);
            if (have >= need)
                continue;

            Popup(ent, args.Actor, "fs-field-kit-short", ("amount", (need - have).Int()), ("reagent", ReagentName(reagent)));
            return;
        }

        foreach (var (reagent, reactant) in reaction.Reactants)
        {
            if (reactant.Catalyst)
                continue;

            var need = reactant.Amount * batches;
            if (reagent == ent.Comp.TissueReagent.Id)
                _solutions.RemoveReagent(tank, reagent, need);
            else
                DrawFromStorage(ent, reagent, need);
        }

        foreach (var (product, amount) in reaction.Products)
            _solutions.TryAddReagent(jug, product, amount * batches);

        _attribution.TagProducer(jugItem, args.Actor);
        Click(ent);
        UpdateUi(ent);
    }

    private void OnDispense(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitDispenseMessage args)
    {
        if (args.Amount <= 0 || !Ready(ent, args.Actor, out _, out var jug, out var jugSolution))
            return;

        var amount = FixedPoint2.Min(args.Amount, jugSolution.AvailableVolume);
        if (args.Location is not { } location)
        {
            if (TryGetTank(ent, out var tank, out _))
            {
                var taken = _solutions.RemoveReagent(tank, ent.Comp.TissueReagent.Id, amount);
                _solutions.TryAddReagent(jug, ent.Comp.TissueReagent.Id, taken);
            }
        }
        else if (TryComp<StorageComponent>(ent, out var storage)
                 && storage.StoredItems.FirstOrDefault(kv => kv.Value == location).Key is { Valid: true } stored
                 && _solutions.TryGetDrainableSolution(stored, out var source, out _))
        {
            _openable.SetOpen(stored, true);
            _solutions.TryAddSolution(jug, _solutions.SplitSolution(source.Value, amount));
        }

        Click(ent);
        UpdateUi(ent);
    }

    // Tissue goes back to the tank rather than down the drain.
    private void OnDiscard(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitDiscardMessage args)
    {
        if (!TryGetJug(ent, out _, out var jug, out var jugSolution))
            return;

        var removed = _solutions.RemoveReagent(jug, args.Reagent, jugSolution.GetTotalPrototypeQuantity(args.Reagent));
        if (args.Reagent == ent.Comp.TissueReagent.Id)
            ReturnTissue(ent, removed);

        UpdateUi(ent);
    }

    private void OnEmptyJug(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitEmptyJugMessage args)
    {
        if (!TryGetJug(ent, out _, out var jug, out var jugSolution))
            return;

        ReturnTissue(ent, jugSolution.GetTotalPrototypeQuantity(ent.Comp.TissueReagent.Id));
        _solutions.RemoveAllSolution(jug);
        UpdateUi(ent);
    }

    private void OnEject(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitEjectMessage args)
    {
        var id = args.Magazine ? ent.Comp.MagazineSlot : ent.Comp.JugSlot;
        if (FSItemSlots.TryGetSlot(EntityManager, _slots, ent, id, out var slot))
            _slots.TryEjectToHands(ent, slot, args.Actor, excludeUserAudio: true);

        UpdateUi(ent);
    }

    private void OnLabel(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitLabelMessage args)
    {
        var label = args.Label.Trim();
        if (label.Length > SharedChemMaster.LabelMaxLength)
            label = label[..(int) SharedChemMaster.LabelMaxLength];

        ent.Comp.Label = label;
        UpdateUi(ent);
    }

    private void ReturnTissue(Entity<FSFieldKitStationComponent> ent, FixedPoint2 amount)
    {
        if (amount > 0 && TryGetTank(ent, out var tank, out var tankSolution))
            _solutions.TryAddReagent(tank, ent.Comp.TissueReagent.Id, FixedPoint2.Min(amount, tankSolution.AvailableVolume));
    }

    private FixedPoint2 StoredQuantity(Entity<FSFieldKitStationComponent> ent, string reagent)
    {
        var total = FixedPoint2.Zero;
        foreach (var source in StoredSources(ent))
            total += source.Comp.Solution.GetTotalPrototypeQuantity(reagent);
        return total;
    }

    private void DrawFromStorage(Entity<FSFieldKitStationComponent> ent, string reagent, FixedPoint2 amount)
    {
        foreach (var source in StoredSources(ent))
        {
            if (amount <= 0)
                return;
            amount -= _solutions.RemoveReagent(source, reagent, amount);
        }
    }

    private IEnumerable<Entity<SolutionComponent>> StoredSources(Entity<FSFieldKitStationComponent> ent)
    {
        if (!TryComp<StorageComponent>(ent, out var storage))
            yield break;

        foreach (var stored in storage.StoredItems.Keys)
        {
            if (_solutions.TryGetDrainableSolution(stored, out var source, out _))
                yield return source.Value;
        }
    }

    private bool Ready(Entity<FSFieldKitStationComponent> ent, EntityUid user, out EntityUid jugItem, out Entity<SolutionComponent> jug, out Solution jugSolution)
    {
        if (!this.IsPowered(ent, EntityManager))
        {
            Popup(ent, user, "fs-field-kit-unpowered");
            jugItem = default;
            jug = default;
            jugSolution = default!;
            return false;
        }

        if (TryGetJug(ent, out jugItem, out jug, out jugSolution))
            return true;

        Popup(ent, user, "fs-field-kit-no-jug");
        return false;
    }

    private bool TryGetJug(Entity<FSFieldKitStationComponent> ent, out EntityUid item, out Entity<SolutionComponent> jug, out Solution solution)
    {
        item = default;
        jug = default;
        solution = default!;
        if (!FSItemSlots.TryGetSlot(EntityManager, _slots, ent, ent.Comp.JugSlot, out var slot)
            || slot.Item is not { } held
            || !_solutions.TryGetFitsInDispenser(held, out var found, out var foundSolution))
            return false;

        item = held;
        jug = found.Value;
        solution = foundSolution;
        return true;
    }

    private bool TryGetTank(Entity<FSFieldKitStationComponent> ent, out Entity<SolutionComponent> tank, out Solution solution)
    {
        tank = default;
        solution = default!;
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.TankSolution, out var found, out var foundSolution))
            return false;

        tank = found.Value;
        solution = foundSolution;
        return true;
    }

    private string ReagentName(string reagent)
        => _prototypes.TryIndex<Content.Shared.Chemistry.Reagent.ReagentPrototype>(reagent, out var proto) ? proto.LocalizedName : reagent;

    private void Popup(EntityUid station, EntityUid user, string key, params (string, object)[] args)
        => _popup.PopupEntity(Loc.GetString(key, args), station, user);

    private void Click(EntityUid station)
        => _audio.PlayPvs(ClickSound, station, AudioParams.Default.WithVolume(-2f));

    private void UpdateUi(Entity<FSFieldKitStationComponent> ent)
    {
        if (!_ui.HasUi(ent, FSFieldKitStationUiKey.Key))
            return;

        var state = new FSFieldKitStationBuiState
        {
            Label = ent.Comp.Label,
            Powered = this.IsPowered(ent, EntityManager),
        };

        if (TryGetTank(ent, out _, out var tank))
        {
            state.Tank = tank.Volume;
            state.TankMax = tank.MaxVolume;
        }

        if (TryGetJug(ent, out var jugItem, out _, out var jugSolution))
            state.Jug = new ContainerInfo(Name(jugItem), jugSolution.Volume, jugSolution.MaxVolume) { Reagents = jugSolution.Contents };

        if (TryComp<StorageComponent>(ent, out var storage))
        {
            foreach (var (stored, location) in storage.StoredItems)
            {
                if (!_solutions.TryGetDrainableSolution(stored, out _, out var sol))
                    continue;

                var label = TryComp<LabelComponent>(stored, out var labelComp) && !string.IsNullOrEmpty(labelComp.CurrentLabel)
                    ? labelComp.CurrentLabel
                    : Name(stored);
                var reagentId = sol.Contents.Count == 1 ? sol.Contents[0].Reagent.Prototype : null;
                state.Inventory.Add(new ReagentInventoryItem(location, label, sol.Volume, sol.GetColor(_prototypes), reagentId));
            }
        }

        if (FSItemSlots.TryGetSlot(EntityManager, _slots, ent, ent.Comp.MagazineSlot, out var magSlot)
            && magSlot.Item is { } magazine)
        {
            state.MagazineName = Name(magazine);
            if (_solutions.TryGetSolution(magazine, ent.Comp.MagazineSolution, out _, out var pack))
            {
                state.MagazineVolume = pack.Volume;
                state.MagazineMax = pack.MaxVolume;
            }
        }

        state.Flasks = EmptyFlasks(ent)
            .GroupBy(f => f.Capacity)
            .OrderBy(g => g.Key)
            .Select(g => new FSFlaskStock(g.Key, g.Count()))
            .ToList();

        _ui.SetUiState(ent.Owner, FSFieldKitStationUiKey.Key, state);
    }
}
