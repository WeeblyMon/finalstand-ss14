// Raises events on a body's organs, selected by organ category.

using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Medical.Relay;

public sealed partial class OrganRelaySystem : EntitySystem
{
    [Dependency] private EntityQuery<BodyComponent> _bodyQuery;
    [Dependency] private EntityQuery<OrganComponent> _organQuery;
    [Dependency] private EntityQuery<ChildOrganComponent> _childQuery;

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

    public void RelayEvent<T>(Entity<BodyComponent?> body, T args) where T : IOrganRelayEvent
    {
        var ev = new OrganRelayedEvent<T>(args);
        RaiseOnTargets(body, args, ev);
    }

    public void RelayEvent<T>(Entity<BodyComponent?> body, ref T args) where T : IOrganRelayEvent
    {
        var ev = new OrganRelayedEvent<T>(args);
        RaiseOnTargets(body, args, ev);
        args = ev.Args;
    }

    private void RaiseOnTargets<T>(Entity<BodyComponent?> body, T args, OrganRelayedEvent<T> ev)
        where T : IOrganRelayEvent
    {
        foreach (var category in args.TargetCategories)
        {
            foreach (var organ in EnumerateOrgansOfCategory(body, category))
            {
                RaiseLocalEvent(organ.Owner, ev);

                if (args.RaiseOnParent
                    && _childQuery.TryComp(organ.Owner, out var child)
                    && child.Parent is { } parent)
                {
                    RaiseLocalEvent(parent, ev);
                }
            }
        }
    }
}
