// A Scavenger supply cache; only its owner can see or pick it up.
namespace Content.Shared._FinalStand.Perks;

[RegisterComponent]
public sealed partial class FSScavengerCacheComponent : Component
{
    public EntityUid? OwnerMind;
}
