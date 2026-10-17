namespace Content.Shared._FinalStand.Perks;

// Only the owner can see or pick up their Scavenger cache.
[RegisterComponent]
public sealed partial class FSScavengerCacheComponent : Component
{
    public EntityUid? OwnerMind;
}
