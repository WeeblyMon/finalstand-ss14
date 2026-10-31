namespace Content.Server._FinalStand.Weapons.Prediction;

[RegisterComponent]
[Access(typeof(FSLagCompensationSystem))]
public sealed partial class FSRewoundProjectileComponent : Component
{
    [ViewVariables]
    public EntityUid Shooter;

    [ViewVariables]
    public double RewindTicks;

    // What the bolt overlapped on the last sweep. Like a physics contact, a target is hit again only after the
    // bolt has left it, so a bolt that bounces back through it lands a second hit.
    [ViewVariables]
    public readonly HashSet<EntityUid> Touching = new();
}
