namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
[Access(typeof(FSUndyingSystem), typeof(FSPerkBodySystem))]
public sealed partial class FSUndyingComponent : Component
{
    public int Level;

    /// <summary>Once per life: set when Undying triggers, cleared when the body is rejuvenated (respawn).</summary>
    public bool Used;

    /// <summary>When the player dies. Null while Undying is not active.</summary>
    public TimeSpan? EndTime;

    public int ShownSeconds;
}
