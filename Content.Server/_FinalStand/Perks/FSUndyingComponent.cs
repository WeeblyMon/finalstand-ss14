namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
public sealed partial class FSUndyingComponent : Component
{
    public int Level;

    /// <summary>Once per life: set when Undying triggers, and never cleared for this body.</summary>
    public bool Used;

    /// <summary>When the player dies. Null while Undying is not active.</summary>
    public TimeSpan? EndTime;

    public int ShownSeconds;
}
