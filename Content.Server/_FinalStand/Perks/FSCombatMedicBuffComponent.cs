namespace Content.Server._FinalStand.Perks;

/// <summary>Combat Medic damage buff, on the mind of both the healer and the healed ally.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FSCombatMedicBuffComponent : Component
{
    [AutoPausedField] public TimeSpan EndTime;
    public int Level;
}
