namespace Content.Server._FinalStand.Perks;

// Static Discharge cooldown, on the mind.
[RegisterComponent]
[Access(typeof(FSStaticDischargeSystem))]
public sealed partial class FSStaticDischargeComponent : Component
{
    public TimeSpan NextReady;
}
