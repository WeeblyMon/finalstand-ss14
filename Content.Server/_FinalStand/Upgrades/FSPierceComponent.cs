namespace Content.Server._FinalStand.Upgrades;

[RegisterComponent]
public sealed partial class FSPierceComponent : Component
{
    public int RemainingPierces;
    public readonly HashSet<EntityUid> AlreadyHit = new();
}
