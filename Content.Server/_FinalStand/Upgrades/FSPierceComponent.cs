namespace Content.Server._FinalStand.Upgrades;

[RegisterComponent]
public sealed partial class FSPierceComponent : Component
{
    public int RemainingPierces;

    [DataField]
    public float DamageRetained = 0.5f;

    public readonly HashSet<EntityUid> AlreadyHit = new();
}
