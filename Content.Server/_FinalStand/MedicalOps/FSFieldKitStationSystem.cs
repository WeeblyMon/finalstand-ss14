using System.Linq;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Utility;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
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
    [Dependency] private OpenableSystem _openable = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private FSTreatmentAttributionSystem _attribution = default!;
    [Dependency] private FSChemLauncherSystem _launcher = default!;
    [Dependency] private FSTonicCauldronSystem _tonic = default!;
    [Dependency] private FSMedicalRosterSystem _roster = default!;
    [Dependency] private WaveGameRuleSystem _wave = default!;

    private static readonly SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSFieldKitStationComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<FSFieldKitStationComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<FSFieldKitStationComponent, BoundUIOpenedEvent>(OnUiOpened);

        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitSetSlotMessage>(OnSetSlot);
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitBrewTonicMessage>(OnBrewTonic);
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitDispenseMessage>(OnDispense);
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitDiscardMessage>(OnDiscard);
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitEmptyJugMessage>(OnEmptyJug);
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitEjectMessage>(OnEject);
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitLabelMessage>(OnLabel);

        SubscribeLocalEvent<WavePrepStartedEvent>(_ => RefreshAll());
        SubscribeLocalEvent<WaveCombatStartedEvent>(_ => RefreshAll());

        InitializePackaging();
    }

    private void OnContainerChanged<T>(Entity<FSFieldKitStationComponent> ent, ref T args) => UpdateUi(ent);

    private void OnUiOpened(Entity<FSFieldKitStationComponent> ent, ref BoundUIOpenedEvent args) => UpdateUi(ent);

    private void RefreshAll()
    {
        var query = EntityQueryEnumerator<FSFieldKitStationComponent>();
        while (query.MoveNext(out var uid, out var comp))
            UpdateUi((uid, comp));
    }

    private void OnSetSlot(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitSetSlotMessage args)
    {
        if (_launcher.TrySetSlot(args.Actor, args.Slot, args.Formula))
            Click(ent);
    }

    private void OnBrewTonic(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitBrewTonicMessage args)
    {
        if (!_roster.IsChemist(args.Actor))
        {
            Popup(ent, args.Actor, "fs-tonic-chemist-only");
            return;
        }

        if (_tonic.TryBrew(args.Actor, Transform(ent).Coordinates, ent.Comp.TonicPrototype))
            Click(ent);
    }

    private void OnDispense(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitDispenseMessage args)
    {
        if (args.Amount <= 0 || !Ready(ent, args.Actor, out _, out var jug, out var jugSolution))
            return;

        var amount = FixedPoint2.Min(args.Amount, jugSolution.AvailableVolume);
        var location = args.Location;
        if (TryComp<StorageComponent>(ent, out var storage)
            && storage.StoredItems.FirstOrDefault(kv => kv.Value == location).Key is { Valid: true } stored
            && _solutions.TryGetDrainableSolution(stored, out var source, out _))
        {
            _openable.SetOpen(stored, true);
            _solutions.TryAddSolution(jug, _solutions.SplitSolution(source.Value, amount));
        }

        Click(ent);
        UpdateUi(ent);
    }

    private void OnDiscard(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitDiscardMessage args)
    {
        if (!TryGetJug(ent, out _, out var jug, out var jugSolution))
            return;

        _solutions.RemoveReagent(jug, args.Reagent, jugSolution.GetTotalPrototypeQuantity(args.Reagent));
        UpdateUi(ent);
    }

    private void OnEmptyJug(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitEmptyJugMessage args)
    {
        if (!TryGetJug(ent, out _, out var jug, out _))
            return;

        _solutions.RemoveAllSolution(jug);
        UpdateUi(ent);
    }

    private void OnEject(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitEjectMessage args)
    {
        if (FSItemSlots.TryGetSlot(EntityManager, _slots, ent, ent.Comp.JugSlot, out var slot))
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

        state.Prep = !_wave.TryGetWaveStatus(out state.Wave, out var phase) || phase == WavePhase.Prep;

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

        _ui.SetUiState(ent.Owner, FSFieldKitStationUiKey.Key, state);
    }
}
