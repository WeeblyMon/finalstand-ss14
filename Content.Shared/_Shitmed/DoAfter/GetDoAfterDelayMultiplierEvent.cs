using Content.Shared._FinalStand.Medical;
using Content.Shared._FinalStand.Medical.Relay;
using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.DoAfter;

public sealed class GetDoAfterDelayMultiplierEvent(
    float multiplier = 1f,
    ProtoId<OrganCategoryPrototype>[]? hands = null)
    : EntityEventArgs, IOrganRelayEvent
{
    public float Multiplier = multiplier;

    public ProtoId<OrganCategoryPrototype>[] TargetCategories => hands ?? OrganCategories.Hands;

    public bool RaiseOnParent => true;
}
