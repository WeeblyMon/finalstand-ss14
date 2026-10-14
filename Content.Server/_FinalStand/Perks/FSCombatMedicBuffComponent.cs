namespace Content.Server._FinalStand.Perks;

/// <summary>Combat Medic damage buff, on the mind of both the healer and the healed ally.</summary>
[RegisterComponent]
public sealed partial class FSCombatMedicBuffComponent : Component
{
    public TimeSpan EndTime;
    public int Level;
}
