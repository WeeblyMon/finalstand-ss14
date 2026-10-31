namespace Content.Server._FinalStand.Perks;

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(FSStunningStrikesSystem))]
public sealed partial class FSStunningStrikesCooldownComponent : Component
{
    [AutoPausedField] public TimeSpan NextAllowed;
}
