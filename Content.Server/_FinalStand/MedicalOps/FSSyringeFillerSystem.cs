using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Utility;
using Content.Shared.Audio;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

public sealed class FSSyringeFillerSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSSyringeFillerComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<FSSyringeFillerComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<FSSyringeFillerComponent, FSSyringeFillerFillMessage>(OnFillPressed);
        SubscribeLocalEvent<FSSyringeFillerComponent, FSSyringeFillerPurgeMessage>(OnPurgePressed);
        SubscribeLocalEvent<FSSyringeFillerComponent, FSSyringeFillerEjectMessage>(OnEjectPressed);
        SubscribeLocalEvent<FSSyringeFillerComponent, BoundUIOpenedEvent>(OnUiOpened);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSSyringeFillerComponent>();
        while (query.MoveNext(out var uid, out var filler))
        {
            if (filler.FinishAt is not { } finish || _timing.CurTime < finish)
                continue;

            filler.FinishAt = null;
            Dirty(uid, filler);
            _ambient.SetAmbience(uid, false);
            Complete((uid, filler));
        }
    }

    private void OnInserted(Entity<FSSyringeFillerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!IsOwnSlot(ent, args.Container.ID))
            return;

        TryBegin(ent, popup: true);
        UpdateUi(ent);
    }

    private void OnRemoved(Entity<FSSyringeFillerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (!IsOwnSlot(ent, args.Container.ID))
            return;

        if (ent.Comp.FinishAt != null)
        {
            ent.Comp.FinishAt = null;
            Dirty(ent);
            _ambient.SetAmbience(ent.Owner, false);
        }

        UpdateUi(ent);
    }

    private void OnUiOpened(Entity<FSSyringeFillerComponent> ent, ref BoundUIOpenedEvent args) => UpdateUi(ent);

    private void OnFillPressed(Entity<FSSyringeFillerComponent> ent, ref FSSyringeFillerFillMessage args)
    {
        TryBegin(ent, popup: false);
        UpdateUi(ent);
    }

    private void OnPurgePressed(Entity<FSSyringeFillerComponent> ent, ref FSSyringeFillerPurgeMessage args)
    {
        if (!TryGetMagazineSolution(ent, out var magSoln, out var magazine) || magazine.Volume <= 0)
            return;

        _solutions.RemoveAllSolution(magSoln);
        _solutions.UpdateChemicals(magSoln);
        _popup.PopupEntity(Loc.GetString("fs-syringe-filler-purged"), ent, args.Actor);

        TryBegin(ent, popup: false);
        UpdateUi(ent);
    }

    private void OnEjectPressed(Entity<FSSyringeFillerComponent> ent, ref FSSyringeFillerEjectMessage args)
    {
        var id = args.Source ? ent.Comp.SourceSlot : ent.Comp.MagazineSlot;
        if (FSItemSlots.TryGetSlot(EntityManager, _slots, ent, id, out var slot))
            _slots.TryEjectToHands(ent, slot, args.Actor, excludeUserAudio: true);

        UpdateUi(ent);
    }

    private bool IsOwnSlot(Entity<FSSyringeFillerComponent> ent, string id) =>
        id == ent.Comp.SourceSlot || id == ent.Comp.MagazineSlot;

    private void TryBegin(Entity<FSSyringeFillerComponent> ent, bool popup)
    {
        if (ent.Comp.FinishAt != null)
            return;

        var transfer = Evaluate(ent, out var status);
        if (transfer <= FixedPoint2.Zero)
        {
            if (popup && status.Length > 0)
                _popup.PopupEntity(status, ent);
            return;
        }

        var seconds = transfer.Float() / Math.Max(1f, ent.Comp.UnitsPerSecond);
        ent.Comp.StartedAt = _timing.CurTime;
        ent.Comp.FinishAt = ent.Comp.StartedAt + TimeSpan.FromSeconds(seconds);
        Dirty(ent);

        _ambient.SetAmbience(ent.Owner, true);
    }

    private FixedPoint2 Evaluate(Entity<FSSyringeFillerComponent> ent, out string status)
    {
        status = string.Empty;

        if (!this.IsPowered(ent, EntityManager))
        {
            status = Loc.GetString("fs-syringe-filler-unpowered");
            return FixedPoint2.Zero;
        }

        var hasSource = TryGetSourceSolution(ent, out _, out var source);
        var hasMagazine = TryGetMagazineSolution(ent, out _, out var magazine);

        if (!hasSource || !hasMagazine)
        {
            status = Loc.GetString(hasMagazine
                ? "fs-syringe-filler-needs-source"
                : "fs-syringe-filler-needs-magazine");
            return FixedPoint2.Zero;
        }

        if (source.Volume <= 0)
        {
            status = Loc.GetString("fs-syringe-filler-source-empty");
            return FixedPoint2.Zero;
        }

        var free = magazine.MaxVolume - magazine.Volume;
        if (free <= 0)
        {
            status = Loc.GetString("fs-syringe-filler-full");
            return FixedPoint2.Zero;
        }

        if (magazine.Volume > 0 && !SameContents(magazine, source))
        {
            status = Loc.GetString("fs-syringe-filler-mixed");
            return FixedPoint2.Zero;
        }

        return FixedPoint2.Min(free, source.Volume);
    }

    private void Complete(Entity<FSSyringeFillerComponent> ent)
    {
        var transfer = Evaluate(ent, out _);
        if (transfer <= FixedPoint2.Zero
            || !TryGetSourceSolution(ent, out var sourceSoln, out _)
            || !TryGetMagazineSolution(ent, out var magSoln, out _))
        {
            UpdateUi(ent);
            return;
        }

        var drawn = _solutions.SplitSolution(sourceSoln, transfer);
        _solutions.TryAddSolution(magSoln, drawn);

        _solutions.UpdateChemicals(magSoln);

        _popup.PopupEntity(Loc.GetString("fs-syringe-filler-done"), ent);
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<FSSyringeFillerComponent> ent)
    {
        if (!_ui.HasUi(ent, FSSyringeFillerUiKey.Key))
            return;

        var state = new FSSyringeFillerBuiState
        {
            Running = ent.Comp.FinishAt != null,
            StartedAt = ent.Comp.StartedAt,
            FinishAt = ent.Comp.FinishAt ?? TimeSpan.Zero,
        };

        if (FSItemSlots.TryGetSlot(EntityManager, _slots, ent, ent.Comp.SourceSlot, out var sourceSlot)
            && sourceSlot.Item is { } container)
        {
            state.SourceName = Name(container);
            if (_solutions.TryGetDrainableSolution(container, out _, out var source))
            {
                state.SourceContents = Describe(source);
                state.SourceVolume = source.Volume.Float();
                state.SourceMax = source.MaxVolume.Float();
            }
        }

        if (FSItemSlots.TryGetSlot(EntityManager, _slots, ent, ent.Comp.MagazineSlot, out var magSlot)
            && magSlot.Item is { } magazine)
        {
            state.MagazineName = Name(magazine);
            if (_solutions.TryGetSolution(magazine, ent.Comp.MagazineSolution, out _, out var pack))
            {
                state.MagazineContents = Describe(pack);
                state.MagazineVolume = pack.Volume.Float();
                state.MagazineMax = pack.MaxVolume.Float();
                state.CanPurge = pack.Volume > 0;
            }

            if (TryComp<SolutionAmmoProviderComponent>(magazine, out var provider))
            {
                state.Darts = provider.Shots;
                state.MaxDarts = provider.MaxShots;
            }
        }

        var transfer = Evaluate(ent, out var status);
        state.CanFill = !state.Running && transfer > FixedPoint2.Zero;

        if (TryGetMagazineSolution(ent, out _, out var mag))
        {
            state.SmallSource = transfer > FixedPoint2.Zero && transfer < mag.MaxVolume - mag.Volume;
            state.MixedBlocked = transfer <= FixedPoint2.Zero
                                 && mag.Volume > 0
                                 && TryGetSourceSolution(ent, out _, out var src)
                                 && src.Volume > 0
                                 && !SameContents(mag, src);
        }

        state.Status = state.Running
            ? Loc.GetString("fs-syringe-filler-working")
            : transfer > FixedPoint2.Zero
                ? Loc.GetString("fs-syringe-filler-ready", ("units", transfer.Int()))
                : status;

        _ui.SetUiState(ent.Owner, FSSyringeFillerUiKey.Key, state);
    }

    private string Describe(Solution solution)
    {
        if (solution.Volume <= 0)
            return Loc.GetString("fs-syringe-filler-empty");

        if (solution.GetPrimaryReagentId() is not { } primary
            || !_prototypes.TryIndex<ReagentPrototype>(primary.Prototype, out var proto))
        {
            return Loc.GetString("fs-syringe-filler-unknown");
        }

        return solution.Contents.Count > 1
            ? Loc.GetString("fs-syringe-filler-mixture", ("reagent", proto.LocalizedName))
            : proto.LocalizedName;
    }

    private static bool SameContents(Solution magazine, Solution source)
    {
        foreach (var reagent in magazine.Contents)
        {
            if (!source.ContainsPrototype(reagent.Reagent.Prototype))
                return false;
        }

        foreach (var reagent in source.Contents)
        {
            if (!magazine.ContainsPrototype(reagent.Reagent.Prototype))
                return false;
        }

        return true;
    }

    private bool TryGetSourceSolution(Entity<FSSyringeFillerComponent> ent,
        out Entity<SolutionComponent> soln, out Solution solution)
    {
        soln = default;
        solution = default!;

        if (!FSItemSlots.TryGetSlot(EntityManager, _slots, ent, ent.Comp.SourceSlot, out var slot)
            || slot.Item is not { } container)
        {
            return false;
        }

        if (!_solutions.TryGetDrainableSolution(container, out var drainable, out var found))
            return false;

        soln = drainable.Value;
        solution = found;
        return true;
    }

    private bool TryGetMagazineSolution(Entity<FSSyringeFillerComponent> ent,
        out Entity<SolutionComponent> soln, out Solution solution)
    {
        soln = default;
        solution = default!;

        if (!FSItemSlots.TryGetSlot(EntityManager, _slots, ent, ent.Comp.MagazineSlot, out var slot)
            || slot.Item is not { } magazine)
        {
            return false;
        }

        if (!_solutions.TryGetSolution(magazine, ent.Comp.MagazineSolution, out var packSoln, out var found))
            return false;

        soln = packSoln.Value;
        solution = found;
        return true;
    }
}
