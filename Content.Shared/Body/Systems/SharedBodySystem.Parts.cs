using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Movement.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

// Shitmed Change Start
using Content.Shared._Shitmed.Body.Components;
using Content.Shared._Shitmed.BodyEffects;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Shared.Random;

namespace Content.Shared.Body.Systems;

public partial class SharedBodySystem
{
    private static readonly ProtoId<DamageTypePrototype> BloodlossDamageType = "Bloodloss";

    private void InitializeParts()
    {
        SubscribeLocalEvent<BodyPartComponent, EntInsertedIntoContainerMessage>(OnBodyPartInserted);
        SubscribeLocalEvent<BodyPartComponent, EntRemovedFromContainerMessage>(OnBodyPartRemoved);

        // Shitmed Change
        SubscribeLocalEvent<BodyPartComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BodyPartComponent, ComponentRemove>(OnBodyPartRemove);
    }

    private void OnMapInit(Entity<BodyPartComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.PartType == BodyPartType.Chest)
        {
            _slots.AddItemSlot(ent, ent.Comp.ContainerName, ent.Comp.ItemInsertionSlot);
            Dirty(ent, ent.Comp);
        }

        if (ent.Comp.OnAdd is not null || ent.Comp.OnRemove is not null)
            EnsureComp<BodyPartEffectComponent>(ent);

        foreach (var connection in ent.Comp.Children.Keys)
        {
            Containers.EnsureContainer<ContainerSlot>(ent, GetPartSlotContainerId(connection));
        }

        foreach (var organ in ent.Comp.Organs.Keys)
        {
            Containers.EnsureContainer<ContainerSlot>(ent, GetOrganContainerId(organ));
        }
    }

    private void OnBodyPartRemove(Entity<BodyPartComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.PartType == BodyPartType.Chest)
            _slots.RemoveItemSlot(ent, ent.Comp.ItemInsertionSlot);
    }

    /// <summary>
    ///     Shitmed Change: This function handles dropping the items in an entity's slots if they lose all of a given part.
    /// </summary>
    public void DropSlotContents(Entity<BodyPartComponent> partEnt)
    {
        if (partEnt.Comp.Body is null
            || !TryComp<InventoryComponent>(partEnt.Comp.Body, out var inventory)
            || GetBodyPartCount(partEnt.Comp.Body.Value, partEnt.Comp.PartType) != 1
            || !TryGetPartSlotContainerName(partEnt.Comp.PartType, out var containerNames))
            return;

        foreach (var containerName in containerNames)
        {
            _inventory.DropSlotContents(partEnt.Comp.Body.Value, containerName, inventory);
        }
    }

    // Shitmed Change End
    private void OnBodyPartInserted(Entity<BodyPartComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        var insertedUid = args.Entity;
        var slotId = args.Container.ID;
        // <Shitmed>
        if (slotId == ent.Comp.ContainerName)
        {
            _insideBodyPart.InsertedIntoPart(insertedUid, ent);
            return;
        }
        // </Shitmed>

        var body = ent.Comp.Body; // Shitmed Change
        if (body is null)
            return;

        if (TryComp(insertedUid, out BodyPartComponent? part) && slotId.Contains(PartSlotContainerIdPrefix + GetSlotFromBodyPart(part)))
        {
            AddPart(body.Value, (insertedUid, part), slotId);
            RecursiveBodyUpdate((insertedUid, part), body.Value);
        }

        if (TryComp(insertedUid, out OrganComponent? organ) && slotId.Contains(OrganSlotContainerIdPrefix + organ.SlotId))
        {
            AddOrgan((insertedUid, organ), body.Value, ent);
        }
    }

    private void OnBodyPartRemoved(Entity<BodyPartComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        var removedUid = args.Entity;
        var slotId = args.Container.ID;

        // Shitmed Change Start
        if (slotId == ent.Comp.ContainerName)
        {
            _insideBodyPart.RemovedFromPart(removedUid);
            return;
        }

        if (TryComp(removedUid, out BodyPartComponent? part))
        {
            if (!slotId.Contains(PartSlotContainerIdPrefix + GetSlotFromBodyPart(part)))
                return;

            DebugTools.Assert(part.Body == ent.Comp.Body);

            if (part.Body is not null)
            {
                RemovePart(part.Body.Value, (removedUid, part), slotId);
                RecursiveBodyUpdate((removedUid, part), null);
            }
        }

        if (TryComp(removedUid, out OrganComponent? organ))
        {
            if (!slotId.Contains(OrganSlotContainerIdPrefix + organ.SlotId))
                return;

            DebugTools.Assert(organ.Body == ent.Comp.Body);

            RemoveOrgan((removedUid, organ), ent);
        }
        // Shitmed Change End
    }

    private void RecursiveBodyUpdate(Entity<BodyPartComponent> ent, EntityUid? bodyUid)
    {
        ent.Comp.Body = bodyUid;
        Dirty(ent, ent.Comp);

        foreach (var slotId in ent.Comp.Organs.Keys)
        {
            if (!Containers.TryGetContainer(ent, GetOrganContainerId(slotId), out var container))
                continue;

            foreach (var organ in container.ContainedEntities)
            {
                if (!TryComp(organ, out OrganComponent? organComp))
                    continue;

                Dirty(organ, organComp);

                if (organComp.Body is { Valid: true } oldBodyUid)
                {
                    var removedEv = new OrganRemovedFromBodyEvent(oldBodyUid, ent);
                    RaiseLocalEvent(organ, ref removedEv);
                }

                organComp.Body = bodyUid;
                if (bodyUid is not null)
                {
                    var addedEv = new OrganAddedToBodyEvent(bodyUid.Value, ent);
                    RaiseLocalEvent(organ, ref addedEv);
                }
            }
        }

        foreach (var slotId in ent.Comp.Children.Keys)
        {
            if (!Containers.TryGetContainer(ent, GetPartSlotContainerId(slotId), out var container))
                continue;

            foreach (var containedUid in container.ContainedEntities)
            {
                if (TryComp(containedUid, out BodyPartComponent? childPart))
                    RecursiveBodyUpdate((containedUid, childPart), bodyUid);
            }
        }
    }

    protected virtual void AddPart(
        Entity<BodyComponent?> bodyEnt,
        Entity<BodyPartComponent> partEnt,
        string slotId)
    {
        partEnt.Comp.Body = bodyEnt;
        Dirty(partEnt, partEnt.Comp);
        var ev = new BodyPartAddedEvent(slotId, partEnt);
        RaiseLocalEvent(bodyEnt, ref ev);

        var ev1 = new BodyPartAddedEvent(slotId, partEnt);
        RaiseLocalEvent(partEnt, ref ev1);

        AddLeg(partEnt, bodyEnt);
    }

    protected virtual void RemovePart(
        Entity<BodyComponent?> bodyEnt,
        Entity<BodyPartComponent> partEnt,
        string slotId)
    {
        Resolve(bodyEnt, ref bodyEnt.Comp, logMissing: false);
        Dirty(partEnt, partEnt.Comp);

        var ev = new BodyPartRemovedEvent(slotId, partEnt);
        RaiseLocalEvent(bodyEnt, ref ev);

        var ev1 = new BodyPartRemovedEvent(slotId, partEnt);
        RaiseLocalEvent(partEnt, ref ev1);

        RemoveLeg(partEnt, bodyEnt);
    }

    private void AddLeg(Entity<BodyPartComponent> legEnt, Entity<BodyComponent?> bodyEnt)
    {
        if (!Resolve(bodyEnt, ref bodyEnt.Comp, logMissing: false))
            return;

        if (legEnt.Comp.PartType != BodyPartType.Leg)
            return;

        bodyEnt.Comp.LegEntities.Add(legEnt);
        UpdateMovementSpeed(bodyEnt);
        Dirty(bodyEnt, bodyEnt.Comp);
    }

    private void RemoveLeg(Entity<BodyPartComponent> legEnt, Entity<BodyComponent?> bodyEnt)
    {
        if (!Resolve(bodyEnt, ref bodyEnt.Comp, logMissing: false))
            return;

        if (legEnt.Comp.PartType != BodyPartType.Leg)
            return;

        bodyEnt.Comp.LegEntities.Remove(legEnt);
        UpdateMovementSpeed(bodyEnt);
        Dirty(bodyEnt, bodyEnt.Comp);
        Standing.Down(bodyEnt);
    }

    public EntityUid? GetParentPartOrNull(EntityUid uid)
    {
        if (!Containers.TryGetContainingContainer((uid, null, null), out var container))
            return null;

        var parent = container.Owner;

        if (!HasComp<BodyPartComponent>(parent))
            return null;

        return parent;
    }

    public (EntityUid Parent, string Slot)? GetParentPartAndSlotOrNull(EntityUid uid)
    {
        if (!Containers.TryGetContainingContainer((uid, null, null), out var container))
            return null;

        var slotId = GetPartSlotContainerIdFromContainer(container.ID);

        if (string.IsNullOrEmpty(slotId))
            return null;

        var parent = container.Owner;

        if (!TryComp<BodyPartComponent>(parent, out var parentBody)
            || !parentBody.Children.ContainsKey(slotId))
            return null;

        return (parent, slotId);
    }

    public bool TryGetParentBodyPart(
        EntityUid partUid,
        [NotNullWhen(true)] out EntityUid? parentUid,
        [NotNullWhen(true)] out BodyPartComponent? parentComponent)
    {
        DebugTools.Assert(HasComp<BodyPartComponent>(partUid));
        parentUid = null;
        parentComponent = null;

        if (Containers.TryGetContainingContainer((partUid, null, null), out var container) &&
            TryComp(container.Owner, out parentComponent))
        {
            parentUid = container.Owner;
            return true;
        }

        return false;
    }

    #region Slots

    private BodyPartSlot? CreatePartSlot(
        EntityUid partUid,
        string slotId,
        BodyPartType partType,
        BodyPartSymmetry symmetry,
        BodyPartComponent? part = null)
    {
        if (!Resolve(partUid, ref part, logMissing: false))
            return null;

        Containers.EnsureContainer<ContainerSlot>(partUid, GetPartSlotContainerId(slotId));
        if (part.Children.TryGetValue(slotId, out var existing))
            return existing;

        var partSlot = new BodyPartSlot(slotId, partType, symmetry);
        part.Children.Add(slotId, partSlot);
        Dirty(partUid, part);
        return partSlot;
    }

    public bool TryCreatePartSlot(
        EntityUid? partId,
        string slotId,
        BodyPartType partType,
        BodyPartSymmetry symmetry,
        [NotNullWhen(true)] out BodyPartSlot? slot,
        BodyPartComponent? part = null)
    {
        slot = null;

        if (partId is null
            || !Resolve(partId.Value, ref part, logMissing: false))
        {
            return false;
        }

        Containers.EnsureContainer<ContainerSlot>(partId.Value, GetPartSlotContainerId(slotId));
        slot = new BodyPartSlot(slotId, partType, symmetry);

        if (!part.Children.ContainsKey(slotId)
            && !part.Children.TryAdd(slotId, slot.Value))
            return false;

        Dirty(partId.Value, part);
        return true;
    }

    public bool TryCreatePartSlotAndAttach(
        EntityUid parentId,
        string slotId,
        EntityUid childId,
        BodyPartType partType,
        BodyPartSymmetry symmetry,
        BodyPartComponent? parent = null,
        BodyPartComponent? child = null)
    {
        return TryCreatePartSlot(parentId, slotId, partType, symmetry, out _, parent)
               && AttachPart(parentId, slotId, childId, parent, child);
    }

    #endregion

    #region RootPartManagement

    public bool IsPartRoot(
        EntityUid bodyId,
        EntityUid partId,
        BodyComponent? body = null,
        BodyPartComponent? part = null)
    {
        return Resolve(partId, ref part)
            && Resolve(bodyId, ref body)
            && Containers.TryGetContainingContainer(bodyId, partId, out var container)
            && container.ID == BodyRootContainerId;
    }

    public bool CanAttachToRoot(
        EntityUid bodyId,
        EntityUid partId,
        BodyComponent? body = null,
        BodyPartComponent? part = null)
    {
        return Resolve(bodyId, ref body)
            && Resolve(partId, ref part)
            && body.RootContainer.ContainedEntity is null
            && bodyId != part.Body;
    }

    public bool TryGetRootPart(EntityUid bodyId, [NotNullWhen(true)] out Entity<BodyPartComponent>? rootPart, BodyComponent? body = null)
    {
        rootPart = null;
        if (!Resolve(bodyId, ref body)
            || body.RootContainer?.ContainedEntity is not { } rootContainedEntity
            || !TryComp<BodyPartComponent>(rootContainedEntity, out var bodyPartComponent))
            return false;

        rootPart = (rootContainedEntity, bodyPartComponent);
        return true;
    }

    public bool CanAttachPart(
        EntityUid parentId,
        BodyPartSlot slot,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        return Resolve(partId, ref part, logMissing: false)
            && Resolve(parentId, ref parentPart, logMissing: false)
            && CanAttachPart(parentId, slot.Id, partId, parentPart, part);
    }

    public bool CanAttachPart(
        EntityUid parentId,
        string slotId,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        return Resolve(partId, ref part, logMissing: false)
            && Resolve(parentId, ref parentPart, logMissing: false)
            && parentPart.Children.TryGetValue(slotId, out var parentSlotData)
            && part.PartType == parentSlotData.Type
            && Containers.TryGetContainer(parentId, GetPartSlotContainerId(slotId), out var container)
            && Containers.CanInsert(partId, container);
    }

    public bool CanAttachToSlot(
        EntityUid parentId,
        string slotId,
        BodyPartComponent? parentPart = null)
    {
        return Resolve(parentId, ref parentPart, logMissing: false)
            && parentPart.Children.ContainsKey(slotId);
    }

    public bool AttachPartToRoot(
        EntityUid bodyId,
        EntityUid partId,
        BodyComponent? body = null,
        BodyPartComponent? part = null)
    {
        return Resolve(bodyId, ref body)
            && Resolve(partId, ref part)
            && CanAttachToRoot(bodyId, partId, body, part)
            && Containers.Insert(partId, body.RootContainer);
    }

    #endregion

    #region Attach/Detach

    public bool AttachPart(
        EntityUid parentPartId,
        string slotId,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        return Resolve(parentPartId, ref parentPart, logMissing: false)
            && parentPart.Children.TryGetValue(slotId, out var slot)
            && AttachPart(parentPartId, slot, partId, parentPart, part);
    }

    public bool AttachPart(
        EntityUid parentPartId,
        BodyPartSlot slot,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        if (!Resolve(parentPartId, ref parentPart, logMissing: false)
            || !Resolve(partId, ref part, logMissing: false)
            || !CanAttachPart(parentPartId, slot.Id, partId, parentPart, part)
            || !parentPart.Children.ContainsKey(slot.Id))
        {
            return false;
        }

        if (!Containers.TryGetContainer(parentPartId, GetPartSlotContainerId(slot.Id), out var container))
        {
            DebugTools.Assert($"Unable to find body slot {slot.Id} for {ToPrettyString(parentPartId)}");
            return false;
        }

        part.ParentSlot = slot;

        if (parentPart.Body is { } body
            && TryComp<HumanoidAppearanceComponent>(body, out var humanoid)
            && !humanoid.ProfileLoaded)
        {
            humanoid.ProfileLoaded = true;
            Dirty(body, humanoid);
        }

        return Containers.Insert(partId, container);
    }

    #endregion

    #region Misc

    public void UpdateMovementSpeed(
        EntityUid bodyId,
        BodyComponent? body = null,
        MovementSpeedModifierComponent? movement = null)
    {
        if (!Resolve(bodyId, ref body, ref movement, logMissing: false)
            || body.RequiredLegs <= 0)
        {
            return;
        }

        var walkSpeed = 0f;
        var sprintSpeed = 0f;
        var acceleration = 0f;
        foreach (var legEntity in body.LegEntities)
        {
            if (!TryComp<MovementBodyPartComponent>(legEntity, out var legModifier)
                || HasComp<LimbParalyzedComponent>(legEntity))
                continue;

            walkSpeed += legModifier.WalkSpeed;
            sprintSpeed += legModifier.SprintSpeed;
            acceleration += legModifier.Acceleration;
        }
        walkSpeed /= body.RequiredLegs;
        sprintSpeed /= body.RequiredLegs;
        acceleration /= body.RequiredLegs;
        Movement.ChangeBaseSpeed(bodyId, walkSpeed, sprintSpeed, acceleration, movement);
    }

    #endregion

    #region Queries

    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetPartOrgans(EntityUid partId, BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, logMissing: false))
            yield break;

        foreach (var containerSlotId in part.Organs.Keys.Select(GetOrganContainerId))
        {
            if (!Containers.TryGetContainer(partId, containerSlotId, out var container))
                continue;

            foreach (var containedEnt in container.ContainedEntities)
            {
                if (!TryComp(containedEnt, out OrganComponent? organ))
                    continue;

                yield return (containedEnt, organ);
            }
        }
    }

    public IEnumerable<BaseContainer> GetPartContainers(EntityUid id, BodyPartComponent? part = null)
    {
        if (!Resolve(id, ref part, logMissing: false) ||
            part.Children.Count == 0)
        {
            yield break;
        }

        foreach (var slotId in part.Children.Keys)
        {
            var containerSlotId = GetPartSlotContainerId(slotId);

            if (!Containers.TryGetContainer(id, containerSlotId, out var container))
                continue;

            yield return container;

            foreach (var ent in container.ContainedEntities)
            {
                foreach (var childContainer in GetPartContainers(ent))
                {
                    yield return childContainer;
                }
            }
        }
    }

    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyPartChildren(
        EntityUid partId,
        BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, logMissing: false))
            yield break;

        yield return (partId, part);

        foreach (var slotId in part.Children.Keys)
        {
            var containerSlotId = GetPartSlotContainerId(slotId);

            if (Containers.TryGetContainer(partId, containerSlotId, out var container))
            {
                foreach (var containedEnt in container.ContainedEntities)
                {
                    if (!TryComp(containedEnt, out BodyPartComponent? childPart))
                        continue;

                    foreach (var value in GetBodyPartChildren(containedEnt, childPart))
                    {
                        yield return value;
                    }
                }
            }
        }
    }

    public IEnumerable<BodyPartSlot> GetAllBodyPartSlots(
        EntityUid partId,
        BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, logMissing: false))
            yield break;

        foreach (var (slotId, slot) in part.Children)
        {
            yield return slot;

            var containerSlotId = GetOrganContainerId(slotId);

            if (Containers.TryGetContainer(partId, containerSlotId, out var container))
            {
                foreach (var containedEnt in container.ContainedEntities)
                {
                    if (!TryComp(containedEnt, out BodyPartComponent? childPart))
                        continue;

                    foreach (var subSlot in GetAllBodyPartSlots(containedEnt, childPart))
                    {
                        yield return subSlot;
                    }
                }
            }
        }
    }

    public bool BodyHasPartType(
        EntityUid bodyId,
        BodyPartType type,
        BodyComponent? body = null)
    {
        return GetBodyChildrenOfType(bodyId, type, body).Any();
    }

    public bool PartHasChild(
        EntityUid parentId,
        EntityUid childId,
        BodyPartComponent? parent,
        BodyPartComponent? child)
    {
        if (!Resolve(parentId, ref parent, logMissing: false)
            || !Resolve(childId, ref child, logMissing: false))
        {
            return false;
        }

        foreach (var (foundId, _) in GetBodyPartChildren(parentId, parent))
        {
            if (foundId == childId)
                return true;
        }
        return false;
    }

    public bool BodyHasChild(
        EntityUid bodyId,
        EntityUid partId,
        BodyComponent? body = null,
        BodyPartComponent? part = null)
    {
        return Resolve(bodyId, ref body, logMissing: false)
            && body.RootContainer.ContainedEntity is not null
            && Resolve(partId, ref part, logMissing: false)
            && TryComp(body.RootContainer.ContainedEntity, out BodyPartComponent? rootPart)
            && PartHasChild(body.RootContainer.ContainedEntity.Value, partId, rootPart, part);
    }

    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildrenOfType(
        EntityUid bodyId,
        BodyPartType type,
        BodyComponent? body = null,
        BodyPartSymmetry? symmetry = null)
    {
        foreach (var part in GetBodyChildren(bodyId, body))
        {
            if (part.Component.PartType == type && (symmetry == null || part.Component.Symmetry == symmetry))
                yield return part;
        }
    }

    public List<(EntityUid Owner, T Comp, OrganComponent Organ)> GetBodyPartOrganComponents<T>(
        EntityUid uid,
        BodyPartComponent? part = null)
        where T : IComponent
    {
        if (!Resolve(uid, ref part))
            return new List<(EntityUid owner, T Comp, OrganComponent Organ)>();

        var query = GetEntityQuery<T>();
        var list = new List<(EntityUid Owner, T Comp, OrganComponent Organ)>();

        foreach (var organ in GetPartOrgans(uid, part))
        {
            if (query.TryGetComponent(organ.Id, out var comp))
                list.Add((organ.Id, comp, organ.Component));
        }

        return list;
    }

    public bool TryGetBodyPartOrganComponents<T>(
        EntityUid uid,
        [NotNullWhen(true)] out List<(EntityUid Owner, T Comp, OrganComponent Organ)>? comps,
        BodyPartComponent? part = null)
        where T : IComponent
    {
        if (!Resolve(uid, ref part))
        {
            comps = null;
            return false;
        }

        comps = GetBodyPartOrganComponents<T>(uid, part);

        if (comps.Count != 0)
            return true;

        comps = null;
        return false;
    }

    // Shitmed Change Start
    public bool TryGetBodyPartOrgans(
        EntityUid uid,
        Type type,
        [NotNullWhen(true)] out List<(EntityUid Id, OrganComponent Organ)>? organs,
        BodyPartComponent? part = null)
    {
        if (!Resolve(uid, ref part))
        {
            organs = null;
            return false;
        }

        var list = new List<(EntityUid Id, OrganComponent Organ)>();

        foreach (var organ in GetPartOrgans(uid, part))
        {
            if (HasComp(organ.Id, type))
                list.Add((organ.Id, organ.Component));
        }

        if (list.Count != 0)
        {
            organs = list;
            return true;
        }

        organs = null;
        return false;
    }

    public bool TryGetPartSlotContainerName(BodyPartType partType, out HashSet<string> containerNames)
    {
        containerNames = partType switch
        {
            BodyPartType.Hand => ["gloves"],
            BodyPartType.Foot => ["shoes"],
            BodyPartType.Head => ["eyes", "ears", "head", "mask"],
            _ => [],
        };
        return containerNames.Count > 0;
    }

    public bool TryGetPartFromSlotContainer(string slot, [NotNullWhen(true)] out BodyPartType? partType)
    {
        partType = slot switch
        {
            "innerclothing" or "outerclothing" => BodyPartType.Chest,
            "gloves" => BodyPartType.Hand,
            "shoes" => BodyPartType.Foot,
            "eyes" or "ears" or "head" or "mask" => BodyPartType.Head,
            _ => null,
        };
        return partType is not null;
    }

    public int GetBodyPartCount(EntityUid bodyId, BodyPartType partType, BodyComponent? body = null)
    {
        return !Resolve(bodyId, ref body, logMissing: false) ? 0 : GetBodyChildren(bodyId, body).Count(part => part.Component.PartType == partType);
    }

    public float GetVitalBodyPartRatio(EntityUid bodyId, BodyComponent? body = null)
    {
        if (!Resolve(bodyId, ref body, logMissing: false))
            return 1f;

        var children = GetBodyChildren(bodyId, body);
        var vitalCount = 0;
        var count = 0;
        foreach (var child in children)
        {
            count++;
            if ((int) (child.Component.PartType & BodyPartType.Vital) != 0)
                vitalCount++;
        }

        if (vitalCount == 0)
            return 1f;

        return (float) count / vitalCount;
    }

    public string GetSlotFromBodyPart(BodyPartComponent? part)
    {
        var slotName = "";

        if (part is null)
            return slotName;

        slotName = part.SlotId != "" ? part.SlotId : part.PartType.ToString().ToLower();
        return part.Symmetry != BodyPartSymmetry.None ? $"{part.Symmetry.ToString().ToLower()} {slotName}" : slotName;
    }

    public bool CanDetachPart(
        EntityUid parentId,
        BodyPartSlot slot,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        return Resolve(partId, ref part, logMissing: false)
               && Resolve(parentId, ref parentPart, logMissing: false)
               && CanDetachPart(parentId, slot.Id, partId, parentPart, part);
    }

    public bool CanDetachPart(
        EntityUid parentId,
        string slotId,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        return Resolve(partId, ref part, logMissing: false)
               && Resolve(parentId, ref parentPart, logMissing: false)
               && parentPart.Children.TryGetValue(slotId, out var parentSlotData)
               && part.PartType == parentSlotData.Type
               && Containers.TryGetContainer(parentId, GetPartSlotContainerId(slotId), out var container)
               && Containers.CanRemove(partId, container);
    }

    public bool DetachPart(
        EntityUid parentPartId,
        string slotId,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        return Resolve(parentPartId, ref parentPart, logMissing: false)
               && parentPart.Children.TryGetValue(slotId, out var slot)
               && DetachPart(parentPartId, slot, partId, parentPart, part);
    }

    public bool DetachPart(
        EntityUid parentPartId,
        BodyPartSlot slot,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        if (!Resolve(parentPartId, ref parentPart, logMissing: false)
            || !Resolve(partId, ref part, logMissing: false)
            || !CanDetachPart(parentPartId, slot.Id, partId, parentPart, part)
            || !parentPart.Children.ContainsKey(slot.Id))
        {
            return false;
        }

        if (!Containers.TryGetContainer(parentPartId, GetPartSlotContainerId(slot.Id), out var container))
        {
            DebugTools.Assert($"Unable to find body slot {slot.Id} for {ToPrettyString(parentPartId)}");
            return false;
        }

        return Containers.Remove(partId, container);
    }

    public TargetBodyPart? GetRandomBodyPart(EntityUid target,
        EntityUid attacker,
        TargetingComponent? targetComp = null,
        TargetingComponent? attackerComp = null)
    {
        if (!Resolve(target, ref targetComp, false)
            || !Resolve(attacker, ref attackerComp, false))
            return TargetBodyPart.Chest;

        if (_mobState.IsIncapacitated(target)
            || Standing.IsDown(target))
            return attackerComp.Target;

        var totalWeight = targetComp.TargetOdds[attackerComp.Target].Values.Sum();
        var randomValue = _random.NextFloat() * totalWeight;

        foreach (var (part, weight) in targetComp.TargetOdds[attackerComp.Target])
        {
            if (randomValue <= weight)
                return part;
            randomValue -= weight;
        }

        return TargetBodyPart.Chest;
    }

    public TargetBodyPart GetRandomBodyPart(EntityUid target,
        TargetBodyPart targetPart = TargetBodyPart.Chest,
        TargetingComponent? targetComp = null)
    {
        if (!Resolve(target, ref targetComp, false))
            return TargetBodyPart.Chest;

        if (_mobState.IsIncapacitated(target)
            || Standing.IsDown(target))
            return targetPart;

        var totalWeight = targetComp.TargetOdds[targetPart].Values.Sum();
        var randomValue = _random.NextFloat() * totalWeight;

        foreach (var (part, weight) in targetComp.TargetOdds[targetPart])
        {
            if (randomValue <= weight)
                return part;
            randomValue -= weight;
        }

        return targetPart;
    }

    public TargetBodyPart GetRandomBodyPart(EntityUid target)
    {
        var children = GetVitalBodyChildren(target).ToList();
        if (children.Count == 0)
            return TargetBodyPart.Chest;

        return GetTargetBodyPart(_random.PickAndTake(children));
    }

    public TargetBodyPart GetRandomBodyPart(EntityUid target,
        EntityUid? attacker,
        TargetBodyPart? targetPart = null,
        TargetingComponent? targetComp = null)
    {
        if (!Resolve(target, ref targetComp, false))
            return TargetBodyPart.Chest;

        if (targetPart.HasValue)
            return GetRandomBodyPart(target, targetPart: targetPart.Value);

        if (attacker.HasValue
            && TryComp(attacker.Value, out TargetingComponent? attackerComp))
            return GetRandomBodyPart(target, targetPart: attackerComp.Target);

        return GetRandomBodyPart(target);
    }

    public TargetBodyPart GetTargetBodyPart(EntityUid target,
        EntityUid? attacker,
        TargetBodyPart? targetPart = null,
        TargetingComponent? targetComp = null)
    {
        if (!Resolve(target, ref targetComp, false))
            return TargetBodyPart.Chest;

        if (targetPart.HasValue)
            return targetPart.Value;

        if (attacker.HasValue
            && TryComp(attacker.Value, out TargetingComponent? attackerComp))
            return attackerComp.Target;

        return GetRandomBodyPart(target);
    }

    public TargetBodyPart GetTargetBodyPart(EntityUid partId)
    {
        if (!TryComp(partId, out BodyPartComponent? part))
            return TargetBodyPart.Chest;

        return GetTargetBodyPart(part);
    }
    public TargetBodyPart GetTargetBodyPart(Entity<BodyPartComponent> part)
    {
        return GetTargetBodyPart(part.Comp.PartType, part.Comp.Symmetry);
    }

    public TargetBodyPart GetTargetBodyPart(BodyPartComponent part)
    {
        return GetTargetBodyPart(part.PartType, part.Symmetry);
    }

    public TargetBodyPart GetTargetBodyPart(BodyPartType type, BodyPartSymmetry symmetry)
    {
        return (type, symmetry) switch
        {
            (BodyPartType.Head, _) => TargetBodyPart.Head,
            (BodyPartType.Chest, _) => TargetBodyPart.Chest,
            (BodyPartType.Groin, _) => TargetBodyPart.Groin,
            (BodyPartType.Arm, BodyPartSymmetry.Left) => TargetBodyPart.LeftArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => TargetBodyPart.RightArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => TargetBodyPart.LeftHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => TargetBodyPart.RightHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => TargetBodyPart.LeftLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => TargetBodyPart.RightLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => TargetBodyPart.LeftFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => TargetBodyPart.RightFoot,
            _ => TargetBodyPart.Chest,
        };
    }

    public (BodyPartType Type, BodyPartSymmetry Symmetry) ConvertTargetBodyPart(TargetBodyPart? targetPart)
    {
        return targetPart switch
        {
            TargetBodyPart.Head => (BodyPartType.Head, BodyPartSymmetry.None),
            TargetBodyPart.Chest => (BodyPartType.Chest, BodyPartSymmetry.None),
            TargetBodyPart.Groin => (BodyPartType.Groin, BodyPartSymmetry.None),
            TargetBodyPart.LeftArm => (BodyPartType.Arm, BodyPartSymmetry.Left),
            TargetBodyPart.LeftHand => (BodyPartType.Hand, BodyPartSymmetry.Left),
            TargetBodyPart.RightArm => (BodyPartType.Arm, BodyPartSymmetry.Right),
            TargetBodyPart.RightHand => (BodyPartType.Hand, BodyPartSymmetry.Right),
            TargetBodyPart.LeftLeg => (BodyPartType.Leg, BodyPartSymmetry.Left),
            TargetBodyPart.LeftFoot => (BodyPartType.Foot, BodyPartSymmetry.Left),
            TargetBodyPart.RightLeg => (BodyPartType.Leg, BodyPartSymmetry.Right),
            TargetBodyPart.RightFoot => (BodyPartType.Foot, BodyPartSymmetry.Right),
            _ => (BodyPartType.Chest, BodyPartSymmetry.None)
        };
    }

    public IEnumerable<(EntityUid Id, BodyPartComponent Component, T ExtraComponent)> GetBodyChildrenOfTypeWithComponent<T>(
        EntityUid bodyId,
        BodyPartType type,
        BodyComponent? body = null,
        BodyPartSymmetry? symmetry = null)
        where T : IComponent
    {
        var query = GetEntityQuery<T>();

        foreach (var part in GetBodyChildren(bodyId, body))
        {
            if (part.Component.PartType == type
                && (symmetry == null || part.Component.Symmetry == symmetry)
                && query.TryGetComponent(part.Id, out var extraComponent))
            {
                yield return (part.Id, part.Component, extraComponent);
            }
        }
    }
    // Shitmed Change End

    public IEnumerable<EntityUid> GetBodyPartAdjacentParts(
        EntityUid partId,
        BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, logMissing: false))
            yield break;

        if (TryGetParentBodyPart(partId, out var parentUid, out _))
            yield return parentUid.Value;

        foreach (var containedEnt in part.Children.Keys.Select(slotId => Containers.GetContainer(partId, GetPartSlotContainerId(slotId))).SelectMany(container => container.ContainedEntities))
        {
            yield return containedEnt;
        }
    }

    public IEnumerable<(EntityUid AdjacentId, T Component)> GetBodyPartAdjacentPartsComponents<T>(
        EntityUid partId,
        BodyPartComponent? part = null)
        where T : IComponent
    {
        if (!Resolve(partId, ref part, logMissing: false))
            yield break;

        var query = GetEntityQuery<T>();
        foreach (var adjacentId in GetBodyPartAdjacentParts(partId, part))
        {
            if (query.TryGetComponent(adjacentId, out var component))
                yield return (adjacentId, component);
        }
    }

    public bool TryGetBodyPartAdjacentPartsComponents<T>(
        EntityUid partId,
        [NotNullWhen(true)] out List<(EntityUid AdjacentId, T Component)>? comps,
        BodyPartComponent? part = null)
        where T : IComponent
    {
        if (!Resolve(partId, ref part, logMissing: false))
        {
            comps = null;
            return false;
        }

        var query = GetEntityQuery<T>();
        comps = new List<(EntityUid AdjacentId, T Component)>();

        foreach (var adjacentId in GetBodyPartAdjacentParts(partId, part))
        {
            if (query.TryGetComponent(adjacentId, out var component))
                comps.Add((adjacentId, component));
        }

        if (comps.Count != 0)
            return true;

        comps = null;
        return false;
    }

    #endregion
}
