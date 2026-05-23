namespace Content.Server._FinalStand.Leveling;

[RegisterComponent]
public sealed partial class FSPlayerLevelComponent : Component
{
    public int Level = 1;
    public int Experience = 0;
    public int XpToNextLevel = 500;
    public int PrestigeLevel = 0;
    public float XpMultiplier = 1f;
}
