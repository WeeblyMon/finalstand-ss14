// Queries over a body's organ graph: by category, and by parent/child relation.

using System.Linq;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Medical;

public sealed partial class OrganLookupSystem : EntitySystem
{
    [Dependency] private EntityQuery<BodyComponent> _bodyQuery = default!;
    [Dependency] private EntityQuery<OrganComponent> _organQuery = default!;
    [Dependency] private EntityQuery<ChildOrganComponent> _childQuery = default!;
    [Dependency] private EntityQuery<ParentOrganComponent> _parentQuery = default!;

    public IEnumerable<Entity<OrganComponent>> GetBodyOrgans(Entity<BodyComponent?> body)
    {
        if (!_bodyQuery.Resolve(body, ref body.Comp, false))
            yield break;

        foreach (var organ in body.Comp.Organs?.ContainedEntities ?? [])
        {
            if (_organQuery.TryComp(organ, out var comp))
                yield return (organ, comp);
        }
    }

    public IEnumerable<Entity<OrganComponent>> EnumerateOrgansOfCategory(
        Entity<BodyComponent?> body,
        ProtoId<OrganCategoryPrototype> category)
    {
        foreach (var organ in GetBodyOrgans(body))
        {
            if (organ.Comp.Category == category)
                yield return organ;
        }
    }

    public bool HasOrganOfCategory(Entity<BodyComponent?> body, ProtoId<OrganCategoryPrototype> category)
    {
        using var enumerator = EnumerateOrgansOfCategory(body, category).GetEnumerator();
        return enumerator.MoveNext();
    }

    public int CountOrgansOfCategory(Entity<BodyComponent?> body, ProtoId<OrganCategoryPrototype>? category)
    {
        return category is { } id ? EnumerateOrgansOfCategory(body, id).Count() : 0;
    }

    public bool TryGetRootOrgan(Entity<BodyComponent?> body, out Entity<OrganComponent> root)
    {
        foreach (var organ in EnumerateOrgansOfCategory(body, OrganCategories.Torso))
        {
            root = organ;
            return true;
        }

        root = default;
        return false;
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

    public bool TryGetChildOrgans(EntityUid organ, Type component, out List<Entity<OrganComponent>> organs)
    {
        organs = new List<Entity<OrganComponent>>();
        foreach (var child in EnumerateChildOrgans(organ))
        {
            if (HasComp(child.Owner, component))
                organs.Add(child);
        }

        return organs.Count > 0;
    }

    public bool TryGetParentOrgan(EntityUid organ, out EntityUid parent)
    {
        parent = default;
        if (!_childQuery.TryComp(organ, out var child) || child.Parent is not { } uid)
            return false;

        parent = uid;
        return true;
    }

    public TargetBodyPart? GetTarget(Entity<OrganComponent?> organ)
    {
        return _organQuery.Resolve(organ, ref organ.Comp, false)
            ? OrganCategories.ToTarget(organ.Comp.Category)
            : null;
    }
}
