// Routes an event to a body's organs by category. Replaces Goob's BodyPartType/Symmetry relay.

using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Medical.Relay;

public interface IOrganRelayEvent
{
    ProtoId<OrganCategoryPrototype>[] TargetCategories { get; }

    bool RaiseOnParent { get; }
}
