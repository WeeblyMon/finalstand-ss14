namespace Content.Server._FinalStand.Perks;

// Static Discharge cooldown, on the mind.
[RegisterComponent]
public sealed partial class FSStaticDischargeComponent : Component
{
    public TimeSpan NextReady;
}
