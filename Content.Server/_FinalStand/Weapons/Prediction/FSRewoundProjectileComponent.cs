namespace Content.Server._FinalStand.Weapons.Prediction;

[RegisterComponent]
[Access(typeof(FSLagCompensationSystem))]
public sealed partial class FSRewoundProjectileComponent : Component
{
    [ViewVariables]
    public EntityUid Shooter;

    [ViewVariables]
    public double RewindTicks;

    [ViewVariables]
    public readonly HashSet<EntityUid> Hit = new();
}
