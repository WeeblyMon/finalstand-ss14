namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent]
public sealed partial class FSBoneStaplerComponent : Component
{
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    [ViewVariables]
    public TimeSpan? BaseCooldown;
}
