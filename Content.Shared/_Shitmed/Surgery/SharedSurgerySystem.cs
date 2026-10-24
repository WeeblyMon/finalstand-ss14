
using System.Linq;
using Content.Shared._FinalStand.Medical;
using Content.Shared._FinalStand.Medical.Effects;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Surgery;
using Content.Shared.Buckle.Components;
using Content.Shared.Body;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Content.Shared.Stacks;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Shitmed.Medical.Surgery;

public abstract partial class SharedSurgerySystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private OrganLookupSystem _lookup = default!;
    [Dependency] private Content.Shared.Body.Systems.SharedBodyAppearanceSystem _appearance = default!;
    [Dependency] private OrganManipulationSystem _manipulation = default!;
    [Dependency] private OrganEffectSystem _organEffects = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private RotateToFaceSystem _rotateToFace = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private TraumaSystem _trauma = default!;
    [Dependency] private ConsciousnessSystem _consciousness = default!;
    [Dependency] private PainSystem _pain = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] protected StatusEffectsSystem Status = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private Robust.Shared.Containers.SharedContainerSystem _containers = default!;

    private const float ToolReachRange = 1.5f;

    private EntityQuery<BodyComponent> _bodyQuery;
    private EntityQuery<StackComponent> _stackQuery;

    private readonly Dictionary<EntProtoId, EntityUid> _surgeries = new();

    private readonly List<EntProtoId> _allSurgeries = new();

    public IReadOnlyList<EntProtoId> AllSurgeries => _allSurgeries;

    public override void Initialize()
    {
        base.Initialize();

        _bodyQuery = GetEntityQuery<BodyComponent>();
        _stackQuery = GetEntityQuery<StackComponent>();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<SurgeryTargetComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SurgeryTargetComponent, DoAfterAttemptEvent<SurgeryDoAfterEvent>>(OnBeforeTargetDoAfter);
        SubscribeLocalEvent<SurgeryTargetComponent, SurgeryDoAfterEvent>(OnTargetDoAfter);
        SubscribeLocalEvent<SurgeryCloseIncisionConditionComponent, SurgeryValidEvent>(OnCloseIncisionValid);
        SubscribeLocalEvent<SurgeryHasBodyConditionComponent, SurgeryValidEvent>(OnHasBodyConditionValid);
        SubscribeLocalEvent<SurgeryPartConditionComponent, SurgeryValidEvent>(OnPartConditionValid);
        SubscribeLocalEvent<SurgeryOrganConditionComponent, SurgeryValidEvent>(OnOrganConditionValid);
        SubscribeLocalEvent<SurgeryWoundedConditionComponent, SurgeryValidEvent>(OnWoundedValid);
        SubscribeLocalEvent<SurgeryPartRemovedConditionComponent, SurgeryValidEvent>(OnPartRemovedConditionValid);
        SubscribeLocalEvent<SurgeryOrganSlotConditionComponent, SurgeryValidEvent>(OnOrganSlotConditionValid);
        SubscribeLocalEvent<SurgeryPartPresentConditionComponent, SurgeryValidEvent>(OnPartPresentConditionValid);
        SubscribeLocalEvent<SurgeryTraumaPresentConditionComponent, SurgeryValidEvent>(OnTraumaPresentConditionValid);
        SubscribeLocalEvent<SurgeryBleedsPresentConditionComponent, SurgeryValidEvent>(OnBleedsPresentConditionValid);
        SubscribeLocalEvent<SurgeryMarkingConditionComponent, SurgeryValidEvent>(OnMarkingPresentValid);
        SubscribeLocalEvent<SurgeryBodyComponentConditionComponent, SurgeryValidEvent>(OnBodyComponentConditionValid);
        SubscribeLocalEvent<SurgeryPartComponentConditionComponent, SurgeryValidEvent>(OnPartComponentConditionValid);
        SubscribeLocalEvent<SurgeryOrganOnAddConditionComponent, SurgeryValidEvent>(OnOrganOnAddConditionValid);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<SanitizedComponent, SurgerySanitizationEvent>(OnSanitization);
        SubscribeLocalEvent<SanitizedComponent, HeldRelayedEvent<SurgerySanitizationEvent>>(OnHeldSanitization);

        InitializeSteps();
        InitializeStart();

        LoadPrototypes();
    }

    private void OnHeldSanitization(Entity<SanitizedComponent> ent, ref HeldRelayedEvent<SurgerySanitizationEvent> args)
    {
        if (ent.Comp.WorksInHands)
            args.Args.Handled = true;
    }

    private void OnSanitization(Entity<SanitizedComponent> ent, ref SurgerySanitizationEvent args)
    {
        args.Handled = true;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _surgeries.Clear();
    }

    private void OnMapInit(Entity<SurgeryTargetComponent> ent, ref MapInitEvent args)
    {
        var data = new InterfaceData("SurgeryBui");
        _ui.SetUi(ent.Owner, SurgeryUIKey.Key, data);
    }

    private void OnBeforeTargetDoAfter(Entity<SurgeryTargetComponent> ent,
        ref DoAfterAttemptEvent<SurgeryDoAfterEvent> args)
    {
        if (_net.IsClient
            || !args.Event.Repeat)
            return;

        if (args.Event.Target is not { } target
            || !IsSurgeryValid(ent, target, args.Event.Surgery, args.Event.Step, args.Event.User, out var surgery, out var part, out var _)
            || IsStepComplete(ent, part, args.Event.Step, surgery))
            args.Cancel();
    }

    private void OnTargetDoAfter(Entity<SurgeryTargetComponent> ent, ref SurgeryDoAfterEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (args.Cancelled)
        {
            var failEv = new SurgeryStepFailedEvent(args.User, ent, args.Surgery, args.Step);
            RaiseLocalEvent(args.User, ref failEv);
            return;
        }

        if (args.Target is { } progressPart)
            ClearStepProgress(ent, progressPart, args.Step);

        var tool = EntityUid.Invalid;
        if (args.Handled
            || args.Target is not { } target
            || !IsSurgeryValid(ent, target, args.Surgery, args.Step, args.User, out var surgery, out var part, out var step)
            || !PreviousStepsComplete(ent, part, surgery, args.Step, args.User)
            || !TryFindStepTool(args.User, ent, part, step, out tool, out _, out _, out _))
        {
            if (!args.Handled && _net.IsServer)
                _popup.PopupEntity(Loc.GetString("fs-surgery-refused-changed"), args.User, args.User, PopupType.SmallCaution);

            return;
        }

        var complete = IsStepComplete(ent, part, args.Step, surgery);

        var ev = new SurgeryStepEvent(args.User, ent, part, tool, surgery, step, complete);
        RaiseLocalEvent(step, ref ev);
        RaiseLocalEvent(args.User, ref ev);

        var conditionsHold = SurgeryConditionsHold(ent, part, surgery);
        args.Repeat = HasComp<SurgeryRepeatableStepComponent>(step)
            && conditionsHold
            && !IsStepComplete(ent, part, args.Step, surgery);

        if (_net.IsServer && (!conditionsHold || GetNextStep(ent, part, surgery, args.User) == null))
        {
            var doneEv = new FSSurgeryCompletedEvent(args.User, ent, part, surgery);
            RaiseLocalEvent(ent, ref doneEv, broadcast: true); // FINALSTAND
        }

        if (args.ToolUsed)
        {
            if (_stackQuery.TryComp(tool, out var stack))
                _stack.ReduceCount((tool, stack), 1);
            else
                PredictedQueueDel(tool);
        }

        RefreshUI(ent);
    }

    private void OnCloseIncisionValid(Entity<SurgeryCloseIncisionConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!HasComp<IncisionOpenComponent>(args.Organ) ||
            !HasComp<BleedersClampedComponent>(args.Organ) ||
            !HasComp<SkinRetractedComponent>(args.Organ) ||
            !HasComp<BodyPartReattachedComponent>(args.Organ) ||
            !HasComp<InternalBleedersClampedComponent>(args.Organ))
        {
            args.Cancelled = true;
        }
    }

    private void OnWoundedValid(Entity<SurgeryWoundedConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp(args.Organ, out WoundableComponent? partWoundable))
        {
            args.Cancelled = true;
            return;
        }

        if (_wounds.GetWoundableSeverityPoint(args.Organ, partWoundable, ent.Comp.DamageGroup, healable: true) > 0)
            return;

        if (_wounds.GetWoundableSeverityPoint(args.Organ, partWoundable, ent.Comp.DamageGroup) > 0)
        {
            if (args.IgnoreBlockers)
                return;

            args.Blocker ??= "fs-surgery-tend-wounds-blocked";
        }

        args.Cancelled = true;
    }

    private void OnBodyComponentConditionValid(Entity<SurgeryBodyComponentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        var present = true;
        foreach (var reg in ent.Comp.Components.Values)
        {
            var compType = reg.Component.GetType();
            if (!HasComp(args.Body, compType))
                present = false;
        }

        if (ent.Comp.Inverse ? present : !present)
            args.Cancelled = true;
    }

    private void OnPartComponentConditionValid(Entity<SurgeryPartComponentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        var present = true;
        foreach (var reg in ent.Comp.Components.Values)
        {
            var compType = reg.Component.GetType();
            if (!HasComp(args.Organ, compType))
                present = false;
        }

        args.Cancelled |= present == ent.Comp.Inverse;
    }

    private void OnOrganOnAddConditionValid(Entity<SurgeryOrganOnAddConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<OrganComponent>(args.Organ, out var organ)
            || organ.Body != args.Body)
        {
            args.Cancelled = true;
            return;
        }

        var byCategory = new Dictionary<ProtoId<OrganCategoryPrototype>, OrganEffectComponent>();
        foreach (var child in _lookup.EnumerateChildOrgans<OrganEffectComponent>(args.Organ))
        {
            if (child.Comp1.Category is { } category)
                byCategory[category] = child.Comp2;
        }

        var allOnAddFound = true;
        var zeroOnAddFound = true;

        foreach (var (category, components) in ent.Comp.Components)
        {
            if (!byCategory.TryGetValue(category, out var effect))
                continue;

            foreach (var key in components.Keys)
            {
                if (!effect.Active.ContainsKey(key))
                    allOnAddFound = false;
                else
                    zeroOnAddFound = false;
            }
        }

        if (ent.Comp.Inverse ? allOnAddFound : zeroOnAddFound)
            args.Cancelled = true;
    }

    private void OnHasBodyConditionValid(Entity<SurgeryHasBodyConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (CompOrNull<OrganComponent>(args.Organ)?.Body == null)
            args.Cancelled = true;
    }

    private void OnPartConditionValid(Entity<SurgeryPartConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<OrganComponent>(args.Organ, out var organ))
        {
            args.Cancelled = true;
            return;
        }

        var valid = organ.Category is { } category && ent.Comp.Categories.Contains(category);

        if (ent.Comp.Inverse ? valid : !valid)
            args.Cancelled = true;
    }

    private void OnOrganConditionValid(Entity<SurgeryOrganConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<OrganComponent>(args.Organ, out var organ)
            || organ.Body != args.Body
            || ent.Comp.Organ == null)
        {
            args.Cancelled = true;
            return;
        }

        foreach (var reg in ent.Comp.Organ.Values)
        {
            var matches = _lookup.EnumerateChildOrgans(args.Organ)
                .Where(child => HasComp(child.Owner, reg.Component.GetType()))
                .ToList();

            if (matches.Count == 0)
            {
                if (!ent.Comp.Inverse)
                    args.Cancelled = true;
                continue;
            }

            if (ent.Comp.Inverse
                && (!ent.Comp.Reattaching
                    || !matches.Any(child => HasComp<OrganReattachedComponent>(child.Owner))))
                args.Cancelled = true;
        }
    }

    private void OnOrganSlotConditionValid(Entity<SurgeryOrganSlotConditionComponent> ent, ref SurgeryValidEvent args)
    {
        var occupied = _lookup.EnumerateChildOrgans(args.Organ)
            .Any(child => child.Comp.Category == ent.Comp.Category);

        args.Cancelled |= occupied != ent.Comp.Inverse;
    }

    private void OnPartRemovedConditionValid(Entity<SurgeryPartRemovedConditionComponent> ent, ref SurgeryValidEvent args)
    {
        foreach (var organ in _lookup.EnumerateOrgansOfCategory(args.Body, ent.Comp.Category))
        {
            if (!HasComp<BodyPartReattachedComponent>(organ.Owner))
                args.Cancelled = true;
        }
    }

    private void OnPartPresentConditionValid(Entity<SurgeryPartPresentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (args.Organ == EntityUid.Invalid
            || !HasComp<OrganComponent>(args.Organ))
            args.Cancelled = true;
    }

    private void OnTraumaPresentConditionValid(Entity<SurgeryTraumaPresentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (args.Cancelled)
            return;

        if (_trauma.HasWoundableTrauma(args.Organ, ent.Comp.TraumaType) != ent.Comp.Inverted)
            return;

        if (ent.Comp.Inverted)
        {
            if (args.IgnoreBlockers)
                return;

            args.Blocker ??= ent.Comp.TraumaType == TraumaType.Dismemberment
                ? "fs-surgery-wait-stump"
                : "fs-surgery-wait-trauma";
        }

        args.Cancelled = true;
    }

    private void OnBleedsPresentConditionValid(Entity<SurgeryBleedsPresentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<WoundableComponent>(args.Organ, out var woundable))
        {
            args.Cancelled = true;
            return;
        }

        if (ent.Comp.Inverted != woundable.Bleeds > 0 || HasComp<BleedersClampedComponent>(args.Organ))
            return;

        if (ent.Comp.Inverted)
        {
            if (args.IgnoreBlockers)
                return;

            args.Blocker ??= "fs-surgery-wait-bleeding";
        }

        args.Cancelled = true;
    }

private void OnMarkingPresentValid(Entity<SurgeryMarkingConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<HumanoidProfileComponent>(args.Body, out var appearance))
            return;

        var found = false;

        foreach (var markingList in appearance.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                if (((string)marking.MarkingId).Contains(ent.Comp.MatchString, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (found) break;
        }

        if (ent.Comp.Inverse ? found : !found)
            args.Cancelled = true;
    }

    protected bool IsSurgeryValid(EntityUid body, EntityUid targetPart, EntProtoId surgery, EntProtoId stepId,
        EntityUid user, out Entity<SurgeryComponent> surgeryEnt, out EntityUid part, out EntityUid step)
    {
        surgeryEnt = default;
        part = default;
        step = default;

        if (!HasComp<SurgeryTargetComponent>(body) ||
            !IsLyingDown(body, user) ||
            GetSingleton(surgery) is not { } surgeryEntId ||
            !TryComp(surgeryEntId, out SurgeryComponent? surgeryComp) ||
            !surgeryComp.Steps.Contains(stepId) ||
            GetSingleton(stepId) is not { } stepEnt
            || !HasComp<OrganComponent>(targetPart)
            && !_bodyQuery.HasComp(targetPart))
            return false;

        var ev = new SurgeryValidEvent(body, targetPart);
        if (_timing.IsFirstTimePredicted)
        {
            RaiseLocalEvent(stepEnt, ref ev);
            if (!ev.Cancelled)
                RaiseLocalEvent(surgeryEntId, ref ev);
        }

        if (ev.Cancelled)
            return false;

        surgeryEnt = (surgeryEntId, surgeryComp);
        part = targetPart;
        step = stepEnt;
        return true;
    }

    public bool SurgeryConditionsHold(EntityUid body, EntityUid part, EntityUid surgery)
    {
        var ev = new SurgeryValidEvent(body, part);
        RaiseLocalEvent(surgery, ref ev);
        return !ev.Cancelled;
    }

    public EntityUid? GetSingleton(EntProtoId surgeryOrStep)
    {
        if (!_prototypes.HasIndex(surgeryOrStep))
            return null;

        if (!_surgeries.TryGetValue(surgeryOrStep, out var ent) || TerminatingOrDeleted(ent))
        {
            ent = Spawn(surgeryOrStep, MapCoordinates.Nullspace);
            _surgeries[surgeryOrStep] = ent;
        }

        return ent;
    }

    public bool IsLyingDown(EntityUid entity, EntityUid user)
    {
        if (IsLaidOut(entity))
            return true;

        _popup.PopupClient(Loc.GetString("surgery-error-laying"), user, user);
        return false;
    }

    public bool IsLaidOut(EntityUid entity)
    {
        if (_standing.IsDown(entity))
            return true;

        if (!TryComp<BuckleComponent>(entity, out var buckle))
            return true;

        if (!TryComp<StrapComponent>(buckle.BuckledTo, out var strap))
            return false;

        var rotation = strap.Rotation;
        return rotation.GetCardinalDir() is Direction.West or Direction.East;
    }

    protected virtual void RefreshUI(EntityUid body)
    {
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<EntityPrototype>())
            return;

        LoadPrototypes();
    }

    private void LoadPrototypes()
    {
        foreach (var uid in _surgeries.Values)
        {
            Del(uid);
        }
        _surgeries.Clear();

        _allSurgeries.Clear();
        foreach (var entity in _prototypes.EnumeratePrototypes<EntityPrototype>())
            if (entity.HasComponent<SurgeryComponent>())
                _allSurgeries.Add(new EntProtoId(entity.ID));
    }
}
