namespace Content.Server._FinalStand.Upgrades;

// kill stacks on the AKMS gun; BonusPerStack is set at upgrade purchase based on level
[RegisterComponent]
public sealed partial class FSBattleTranceComponent : Component
{
    public int Stacks = 0;
    public float CurrentBonus = 0f;
    public float BonusPerStack = 0.02f;
    public int MaxStacks = 50;
    public EntityUid? Shooter;
}
