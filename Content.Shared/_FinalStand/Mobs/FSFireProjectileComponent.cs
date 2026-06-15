namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent]
public sealed partial class FSFireProjectileComponent : Component
{
    public readonly HashSet<EntityUid> AlreadyIgnited = new();
}
