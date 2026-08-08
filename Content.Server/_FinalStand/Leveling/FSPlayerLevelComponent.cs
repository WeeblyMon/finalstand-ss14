namespace Content.Server._FinalStand.Leveling;

[RegisterComponent]
public sealed partial class FSPlayerLevelComponent : Component
{
    /// <summary>Set once the saved row has been read onto this mind.</summary>
    public bool Loaded = false;

    public int Level = 1;
    public int Experience = 0;
    public int XpToNextLevel = FSLevelingSystem.XpToNextLevel(1);
    public int PrestigeLevel = 0;
    public float XpMultiplier = 1f;
}
