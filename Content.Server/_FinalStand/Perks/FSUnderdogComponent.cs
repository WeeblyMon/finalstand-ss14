namespace Content.Server._FinalStand.Perks;

// Cached Underdog zombie count on the player's body.
[RegisterComponent]
[Access(typeof(FSUnderdogSystem))]
public sealed partial class FSUnderdogComponent : Component
{
    public int Count;
}
