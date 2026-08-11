// Inserting, removing and re-parenting organs. Replaces Goob's part/organ slot manipulation.

using System.Linq;
using Content.Shared.Body;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Medical;

public sealed partial class OrganManipulationSystem : EntitySystem
{
    [Dependency] private readonly OrganLookupSystem _lookup = default!;
    [Dependency] private readonly OrganRelationSystem _relation = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    [Dependency] private EntityQuery<BodyComponent> _bodyQuery = default!;
    [Dependency] private EntityQuery<OrganComponent> _organQuery = default!;

    public bool CanInsertOrgan(Entity<BodyComponent?> body, ProtoId<OrganCategoryPrototype> category)
    {
        return _bodyQuery.Resolve(body, ref body.Comp, false) && !_lookup.HasOrganOfCategory(body, category);
    }

    // The parent organ decides whether a child of this category fits, e.g. one hand per arm.
    public bool CanAttachOrgan(EntityUid parent, ProtoId<OrganCategoryPrototype> category)
    {
        foreach (var child in _lookup.EnumerateChildOrgans(parent))
        {
            if (child.Comp.Category == category)
                return false;
        }

        return true;
    }

    public bool InsertOrgan(Entity<BodyComponent?> body, Entity<OrganComponent?> organ, EntityUid? parent = null)
    {
        if (!_bodyQuery.Resolve(body, ref body.Comp, false)
            || !_organQuery.Resolve(organ, ref organ.Comp, false)
            || body.Comp.Organs is not { } container)
            return false;

        if (!_container.Insert(organ.Owner, container))
            return false;

        if (parent is { } parentUid && HasComp<ChildOrganComponent>(organ))
            _relation.Relate(parentUid, organ.Owner);

        return true;
    }

    public bool RemoveOrgan(Entity<OrganComponent?> organ)
    {
        if (!_organQuery.Resolve(organ, ref organ.Comp, false) || organ.Comp.Body is not { } body)
            return false;

        if (!_bodyQuery.TryComp(body, out var bodyComp) || bodyComp.Organs is not { } container)
            return false;

        // Detaching a limb takes everything hanging off it with it.
        foreach (var child in _lookup.EnumerateChildOrgans(organ).ToArray())
        {
            RemoveOrgan((child.Owner, child.Comp));
        }

        if (HasComp<ChildOrganComponent>(organ))
            _relation.Orphan(organ.Owner);

        return _container.Remove(organ.Owner, container);
    }

    public bool DropOrgan(Entity<OrganComponent?> organ, EntityCoordinates? coordinates = null)
    {
        if (!RemoveOrgan(organ))
            return false;

        if (coordinates is { } coords)
            _transform.SetCoordinates(organ.Owner, coords);

        return true;
    }

    public bool RemoveOrgan(EntityUid body, Entity<OrganComponent?> organ)
    {
        return RemoveOrgan(organ);
    }

    public bool RemoveOrgan(EntityUid body, EntityUid organ, OrganComponent? organComp)
    {
        return RemoveOrgan((organ, organComp));
    }
}
