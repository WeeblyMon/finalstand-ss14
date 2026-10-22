namespace Content.Server._FinalStand.Perks;

// Static Discharge cooldown, on the mind.
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(FSStaticDischargeSystem))]
public sealed partial class FSStaticDischargeComponent : Component
{
    [AutoPausedField] public TimeSpan NextReady;
}
