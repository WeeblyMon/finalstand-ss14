using Content.Shared._FinalStand.Medical;
using Content.Shared._FinalStand.Medical.Relay;
using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Weapons.Melee.Events;

public sealed class AttemptHandsMeleeEvent(ProtoId<OrganCategoryPrototype>[]? hands = null)
    : CancellableEntityEventArgs, IOrganRelayEvent
{
    public ProtoId<OrganCategoryPrototype>[] TargetCategories => hands ?? OrganCategories.Hands;

    public bool RaiseOnParent => true;

    public bool Handled { get; set; }
}
