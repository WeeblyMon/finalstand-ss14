// Raises events on a body's organs, selected by organ category.

using Content.Shared.Body;

namespace Content.Shared._FinalStand.Medical.Relay;

public sealed partial class OrganRelaySystem : EntitySystem
{
    [Dependency] private OrganLookupSystem _lookup = default!;

    public void RelayEvent<T>(Entity<BodyComponent?> body, T args) where T : class, IOrganRelayEvent
    {
        foreach (var category in args.TargetCategories)
        {
            foreach (var organ in _lookup.EnumerateOrgansOfCategory(body, category))
            {
                RaiseLocalEvent(organ.Owner, args);

                if (args.RaiseOnParent && _lookup.TryGetParentOrgan(organ.Owner, out var parent))
                    RaiseLocalEvent(parent, args);
            }
        }
    }
}
