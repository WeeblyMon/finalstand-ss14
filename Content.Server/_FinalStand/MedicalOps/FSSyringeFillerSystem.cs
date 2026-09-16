using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Utility;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

public sealed class FSSyringeFillerSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSSyringeFillerComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<FSSyringeFillerComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<FSSyringeFillerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
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
            Complete((uid, filler));
        }
    }

    private void OnInserted(Entity<FSSyringeFillerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SourceSlot && args.Container.ID != ent.Comp.MagazineSlot)
            return;

        TryBegin(ent, announce: true);
    }

    private void OnRemoved(Entity<FSSyringeFillerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SourceSlot && args.Container.ID != ent.Comp.MagazineSlot)
            return;

        if (ent.Comp.FinishAt == null)
            return;

        ent.Comp.FinishAt = null;
        Dirty(ent);
    }

    private void OnGetVerbs(Entity<FSSyringeFillerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!TryGetMagazineSolution(ent, out var magSoln, out var magazine) || magazine.Volume <= 0)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fs-syringe-filler-purge-verb"),
            Act = () =>
            {
                _solutions.RemoveAllSolution(magSoln);
                _solutions.UpdateChemicals(magSoln);
                _popup.PopupEntity(Loc.GetString("fs-syringe-filler-purged"), ent, user);
            },
        });
    }

    private void TryBegin(Entity<FSSyringeFillerComponent> ent, bool announce)
    {
        if (ent.Comp.FinishAt != null)
            return;

        if (!this.IsPowered(ent, EntityManager))
        {
            Announce(ent, announce, "fs-syringe-filler-unpowered");
            return;
        }

        if (!TryGetSourceSolution(ent, out _, out var source)
            || !TryGetMagazineSolution(ent, out _, out var magazine))
        {
            return;
        }

        if (source.Volume <= 0)
        {
            Announce(ent, announce, "fs-syringe-filler-source-empty");
            return;
        }

        var free = magazine.MaxVolume - magazine.Volume;
        if (free <= 0)
        {
            Announce(ent, announce, "fs-syringe-filler-full");
            return;
        }

        // A half-and-half magazine is undiagnosable in a fight: the fill tint reads as one reagent
        // and the darts deliver two. Refuse rather than let it be built.
        if (magazine.Volume > 0 && !SameContents(magazine, source))
        {
            Announce(ent, announce, "fs-syringe-filler-mixed");
            return;
        }

        var transfer = FixedPoint2.Min(free, source.Volume);
        var seconds = transfer.Float() / Math.Max(1f, ent.Comp.UnitsPerSecond);

        ent.Comp.FinishAt = _timing.CurTime + TimeSpan.FromSeconds(seconds);
        Dirty(ent);
    }

    private void Complete(Entity<FSSyringeFillerComponent> ent)
    {
        // Both slots are re-resolved: either can be pulled while the machine is running.
        if (!TryGetSourceSolution(ent, out var sourceSoln, out var source)
            || !TryGetMagazineSolution(ent, out var magSoln, out var magazine))
        {
            return;
        }

        var free = magazine.MaxVolume - magazine.Volume;
        var transfer = FixedPoint2.Min(free, source.Volume);
        if (transfer <= FixedPoint2.Zero)
            return;

        if (magazine.Volume > 0 && !SameContents(magazine, source))
            return;

        var drawn = _solutions.SplitSolution(sourceSoln, transfer);
        _solutions.TryAddSolution(magSoln, drawn);

        // SolutionAmmoProvider only recounts on a solution change event. Without this the magazine
        // holds reagent and still reads as empty.
        _solutions.UpdateChemicals(magSoln);

        _popup.PopupEntity(Loc.GetString("fs-syringe-filler-done"), ent);
    }

    private void Announce(EntityUid uid, bool announce, string key)
    {
        if (announce)
            _popup.PopupEntity(Loc.GetString(key), uid);
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
