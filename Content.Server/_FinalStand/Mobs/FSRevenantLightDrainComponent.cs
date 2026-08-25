namespace Content.Server._FinalStand.Mobs;

[RegisterComponent]
public sealed partial class FSRevenantLightDrainComponent : Component
{
    public readonly HashSet<EntityUid> Drained = new();
}
