using Content.Shared._FinalStand.Medical;
using Content.Shared._FinalStand.Medical.Relay;
using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Weapons.Ranged.Events;

public sealed class AttemptHandsShootEvent(ProtoId<OrganCategoryPrototype>[]? hands = null)
    : HandledEntityEventArgs, IOrganRelayEvent
{
    public ProtoId<OrganCategoryPrototype>[] TargetCategories => hands ?? OrganCategories.Hands;

    public bool RaiseOnParent => true;
}
