// Static Discharge cooldown, on the mind.
namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
public sealed partial class FSStaticDischargeComponent : Component
{
    public TimeSpan NextReady;
}
