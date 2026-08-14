// SPDX-FileCopyrightText: 2025 Monolith-Station contributors, Final Stand contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Monolith.Kitchen;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Server.Hands.Systems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Server._Monolith.Kitchen;

public sealed partial class MedicalAssemblerSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPowerStateSystem _powerState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedicalAssemblerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MedicalAssemblerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<MedicalAssemblerComponent, EntInsertedIntoContainerMessage>(OnContentUpdate);
        SubscribeLocalEvent<MedicalAssemblerComponent, EntRemovedFromContainerMessage>(OnContentUpdate);
        SubscribeLocalEvent<MedicalAssemblerComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<MedicalAssemblerComponent, MedicalAssemblerStartMessage>(OnStart);
        SubscribeLocalEvent<MedicalAssemblerComponent, MedicalAssemblerEjectMessage>(OnEject);
        SubscribeLocalEvent<MedicalAssemblerComponent, MedicalAssemblerEjectSolidMessage>(OnEjectSolid);
    }

    private void OnInit(Entity<MedicalAssemblerComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Storage = _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
        SetAppearance(ent, MedicalAssemblerVisualState.Idle);
    }

    private void OnInteractUsing(Entity<MedicalAssemblerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!IsPowered(ent))
        {
            _popup.PopupEntity(Loc.GetString("medical-assembler-no-power"), ent, args.User);
            return;
        }

        if (ent.Comp.IsBusy)
            return;

        if (!HasComp<ItemComponent>(args.Used))
        {
            _popup.PopupEntity(Loc.GetString("medical-assembler-cant-insert"), ent, args.User);
            return;
        }

        if (ent.Comp.Storage.Count >= ent.Comp.Capacity)
        {
            _popup.PopupEntity(Loc.GetString("medical-assembler-full"), ent, args.User);
            return;
        }

        args.Handled = true;
        _hands.TryDropIntoContainer(args.User, args.Used, ent.Comp.Storage);
        UpdateUiState(ent);
    }

    private void OnContentUpdate(EntityUid uid, MedicalAssemblerComponent comp, ContainerModifiedMessage args)
    {
        if (comp.Storage != args.Container)
            return;
        UpdateUiState((uid, comp));
    }

    private void OnPowerChanged(Entity<MedicalAssemblerComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered && HasComp<ActiveMedicalAssemblerComponent>(ent))
            StopAssembling(ent);
        UpdateUiState(ent);
    }

    private void OnStart(Entity<MedicalAssemblerComponent> ent, ref MedicalAssemblerStartMessage args)
    {
        if (ent.Comp.IsBusy || !ent.Comp.Storage.ContainedEntities.Any())
            return;

        if (!IsPowered(ent))
        {
            _popup.PopupEntity(Loc.GetString("medical-assembler-no-power"), ent, args.Actor);
            return;
        }

        var recipe = FindRecipe(ent.Comp);
        if (recipe == null)
        {
            _popup.PopupEntity(Loc.GetString("medical-assembler-no-recipe"), ent, args.Actor);
            _audio.PlayPvs(ent.Comp.ClickSound, ent, AudioParams.Default.WithVolume(-2));
            return;
        }

        ent.Comp.IsBusy = true;
        ent.Comp.CurrentAssembleTimeEnd = _timing.CurTime + TimeSpan.FromSeconds(recipe.AssembleTime);

        var active = AddComp<ActiveMedicalAssemblerComponent>(ent);
        active.TimeRemaining = recipe.AssembleTime;
        active.Recipe = recipe;

        _audio.PlayPvs(ent.Comp.StartSound, ent);
        _powerState.SetWorkingState(ent.Owner, true);
        SetAppearance(ent, MedicalAssemblerVisualState.Assembling);
        UpdateUiState(ent);
    }

    private void OnEject(Entity<MedicalAssemblerComponent> ent, ref MedicalAssemblerEjectMessage args)
    {
        if (ent.Comp.IsBusy)
            return;
        _container.EmptyContainer(ent.Comp.Storage);
        _audio.PlayPvs(ent.Comp.ClickSound, ent, AudioParams.Default.WithVolume(-2));
        UpdateUiState(ent);
    }

    private void OnEjectSolid(Entity<MedicalAssemblerComponent> ent, ref MedicalAssemblerEjectSolidMessage args)
    {
        if (ent.Comp.IsBusy)
            return;
        _container.Remove(GetEntity(args.EntityId), ent.Comp.Storage);
        UpdateUiState(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveMedicalAssemblerComponent, MedicalAssemblerComponent>();
        while (query.MoveNext(out var uid, out var active, out var assembler))
        {
            active.TimeRemaining -= frameTime;
            if (active.TimeRemaining > 0)
                continue;

            FinishAssembling((uid, assembler), active);
        }
    }

    private void FinishAssembling(Entity<MedicalAssemblerComponent> ent, ActiveMedicalAssemblerComponent active)
    {
        if (active.Recipe != null)
        {
            SubtractContents(ent.Comp, active.Recipe);
            var coords = Transform(ent).Coordinates;
            for (var i = 0; i < active.Recipe.ResultCount; i++)
                Spawn(active.Recipe.Result, coords);
        }

        _container.EmptyContainer(ent.Comp.Storage);
        ent.Comp.IsBusy = false;
        ent.Comp.CurrentAssembleTimeEnd = TimeSpan.Zero;
        StopAssembling(ent);
        _audio.PlayPvs(ent.Comp.DoneSound, ent);
        UpdateUiState(ent);
    }

    private void StopAssembling(Entity<MedicalAssemblerComponent> ent)
    {
        RemCompDeferred<ActiveMedicalAssemblerComponent>(ent);
        ent.Comp.IsBusy = false;
        _powerState.SetWorkingState(ent.Owner, false);
        SetAppearance(ent, MedicalAssemblerVisualState.Idle);
    }

    private MedicalAssemblerRecipePrototype? FindRecipe(MedicalAssemblerComponent comp)
    {
        var solids = new Dictionary<string, int>();
        var reagents = new Dictionary<string, FixedPoint2>();

        foreach (var item in comp.Storage.ContainedEntities)
        {
            string? solidId = null;
            var amount = 1;

            if (TryComp<StackComponent>(item, out var stack))
            {
                solidId = _prototype.Index<StackPrototype>(stack.StackTypeId).Spawn;
                amount = stack.Count;
            }
            else
            {
                solidId = MetaData(item).EntityPrototype?.ID;
            }

            if (solidId != null)
            {
                if (!solids.TryAdd(solidId, amount))
                    solids[solidId] += amount;
            }

            if (!_solutionContainer.TryGetDrainableSolution(item, out _, out var solution))
                continue;

            foreach (var (reagent, quantity) in solution.Contents)
            {
                if (!reagents.TryAdd(reagent.Prototype, quantity))
                    reagents[reagent.Prototype] += quantity;
            }
        }

        return _prototype.EnumeratePrototypes<MedicalAssemblerRecipePrototype>()
            .Where(r => CanSatisfy(r, solids, reagents))
            .OrderByDescending(r => r.IngredientsReagents.Count + r.IngredientsSolids.Count)
            .FirstOrDefault();
    }

    private static bool CanSatisfy(
        MedicalAssemblerRecipePrototype recipe,
        Dictionary<string, int> solids,
        Dictionary<string, FixedPoint2> reagents)
    {
        foreach (var (id, needed) in recipe.IngredientsSolids)
        {
            if (!solids.TryGetValue(id, out var have) || have < (int) needed)
                return false;
        }

        foreach (var (id, needed) in recipe.IngredientsReagents)
        {
            if (!reagents.TryGetValue(id, out var have) || have < needed)
                return false;
        }

        return true;
    }

    private void SubtractContents(MedicalAssemblerComponent comp, MedicalAssemblerRecipePrototype recipe)
    {
        var toRemove = new Dictionary<string, FixedPoint2>(recipe.IngredientsReagents);

        foreach (var item in comp.Storage.ContainedEntities)
        {
            if (!_solutionContainer.TryGetDrainableSolution(item, out var solnEnt, out var solution))
                continue;

            foreach (var reagent in recipe.IngredientsReagents.Keys.ToList())
            {
                if (!toRemove.ContainsKey(reagent))
                    continue;

                var have = solution.GetTotalPrototypeQuantity(reagent);
                var remove = FixedPoint2.Min(have, toRemove[reagent]);
                if (remove <= FixedPoint2.Zero)
                    continue;

                _solutionContainer.RemoveReagent(solnEnt!.Value, reagent, remove);
                toRemove[reagent] -= remove;
                if (toRemove[reagent] <= FixedPoint2.Zero)
                    toRemove.Remove(reagent);
            }
        }

        foreach (var (solidId, needed) in recipe.IngredientsSolids)
        {
            var remaining = (int) needed;
            foreach (var item in comp.Storage.ContainedEntities.ToArray())
            {
                if (remaining <= 0)
                    break;

                string? itemId = null;
                if (TryComp<StackComponent>(item, out var stack))
                    itemId = _prototype.Index<StackPrototype>(stack.StackTypeId).Spawn;
                else
                    itemId = MetaData(item).EntityPrototype?.ID;

                if (itemId != solidId)
                    continue;

                if (stack != null)
                {
                    _stack.ReduceCount((item, stack), 1);
                    if (stack.Count == 0)
                        _container.Remove(item, comp.Storage);
                }
                else
                {
                    _container.Remove(item, comp.Storage);
                    Del(item);
                }
                remaining--;
            }
        }
    }

    public void UpdateUiState(Entity<MedicalAssemblerComponent> ent)
    {
        _ui.SetUiState(ent.Owner, MedicalAssemblerUiKey.Key,
            new MedicalAssemblerUpdateUserInterfaceState(
                GetNetEntityArray(ent.Comp.Storage.ContainedEntities.ToArray()),
                ent.Comp.IsBusy,
                ent.Comp.CurrentAssembleTimeEnd));
    }

    private void SetAppearance(Entity<MedicalAssemblerComponent> ent, MedicalAssemblerVisualState state)
    {
        _appearance.SetData(ent, PowerDeviceVisuals.VisualState, state);
    }

    private bool IsPowered(Entity<MedicalAssemblerComponent> ent)
        => TryComp<ApcPowerReceiverComponent>(ent, out var apc) && apc.Powered;
}
