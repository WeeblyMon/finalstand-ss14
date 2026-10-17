using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Medical.Relay;

// Replaces Goob's BodyPartType/Symmetry relay.
public interface IOrganRelayEvent
{
    ProtoId<OrganCategoryPrototype>[] TargetCategories { get; }

    bool RaiseOnParent { get; }
}
