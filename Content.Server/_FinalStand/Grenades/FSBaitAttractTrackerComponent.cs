namespace Content.Server._FinalStand.Grenades;

[RegisterComponent]
public sealed partial class FSBaitAttractTrackerComponent : Component
{
    // zombie uid -> the bait it is currently chasing
    public Dictionary<EntityUid, EntityUid> ZombieToBait = new();
}
