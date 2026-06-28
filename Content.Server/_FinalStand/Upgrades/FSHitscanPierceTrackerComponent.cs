namespace Content.Server._FinalStand.Upgrades;

[RegisterComponent]
public sealed partial class FSHitscanPierceTrackerComponent : Component
{
    public bool PierceInitialized;
    public int RemainingPierces;
}
