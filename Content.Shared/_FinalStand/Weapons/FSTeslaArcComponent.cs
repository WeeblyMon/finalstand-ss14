using Content.Shared.Damage;

namespace Content.Shared._FinalStand.Weapons;

[RegisterComponent]
public sealed partial class FSTeslaArcComponent : Component
{
    [DataField] public float ArcInterval = 0.3f;
    [DataField] public float ArcRange = 6f;
    [DataField] public int MaxArcs = 3;
    [DataField] public int MaxTotalArcs = int.MaxValue;
    [DataField] public DamageSpecifier Damage = new();

    public double NextArcTime;
    public int TotalArcsFired;
    public EntityUid? Shooter;
}
