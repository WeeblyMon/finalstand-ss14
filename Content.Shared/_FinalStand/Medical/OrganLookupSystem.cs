// Queries over a body's organ graph: by category, and by parent/child relation.

using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Medical;

public sealed partial class OrganLookupSystem : EntitySystem
{
    [Dependency] private EntityQuery<BodyComponent> _bodyQuery;
    [Dependency] private EntityQuery<OrganComponent> _organQuery;
    [Dependency] private EntityQuery<ChildOrganComponent> _childQuery;
    [Dependency] private EntityQuery<ParentOrganComponent> _parentQuery;

    public IEnumerable<Entity<OrganComponent>> EnumerateOrgansOfCategory(
        Entity<BodyComponent?> body,
        ProtoId<OrganCategoryPrototype> category)
    {
        if (!_bodyQuery.Resolve(body, ref body.Comp, false))
            yield break;

        foreach (var organ in body.Comp.Organs?.ContainedEntities ?? [])
        {
            if (_organQuery.TryComp(organ, out var comp) && comp.Category == category)
                yield return (organ, comp);
        }
    }

    public bool HasOrganOfCategory(Entity<BodyComponent?> body, ProtoId<OrganCategoryPrototype> category)
    {
        using var enumerator = EnumerateOrgansOfCategory(body, category).GetEnumerator();
        return enumerator.MoveNext();
    }

    public IEnumerable<Entity<OrganComponent>> EnumerateChildOrgans(EntityUid organ)
    {
        if (!_parentQuery.TryComp(organ, out var parent))
            yield break;

        foreach (var child in parent.Children)
        {
            if (_organQuery.TryComp(child, out var comp))
                yield return (child, comp);
        }
    }

    public IEnumerable<Entity<OrganComponent, T>> EnumerateChildOrgans<T>(EntityUid organ) where T : IComponent
    {
        foreach (var child in EnumerateChildOrgans(organ))
        {
            if (TryComp<T>(child.Owner, out var comp))
                yield return (child.Owner, child.Comp, comp);
        }
    }

    public bool TryGetParentOrgan(EntityUid organ, out EntityUid parent)
    {
        parent = default;
        if (!_childQuery.TryComp(organ, out var child) || child.Parent is not { } uid)
            return false;

        parent = uid;
        return true;
    }
}
