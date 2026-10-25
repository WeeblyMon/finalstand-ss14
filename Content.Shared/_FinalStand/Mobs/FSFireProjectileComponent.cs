namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent]
public sealed partial class FSFireProjectileComponent : Component
{
    [DataField] public float FireStacks = 0.1f;

    public readonly HashSet<EntityUid> AlreadyIgnited = new();

    public float CheckAccumulator;
}
