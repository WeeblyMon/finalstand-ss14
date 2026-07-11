namespace Content.Shared._FinalStand.Upgrades.Effects;

[RegisterComponent]
public sealed partial class FSIronBeastBuffComponent : Component
{
    public double LastFireTime;
    public const float FireTimeout = 0.4f;
}
