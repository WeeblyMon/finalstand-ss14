namespace Content.Server._FinalStand.Mobs;

[RegisterComponent]
[Access(typeof(FSRevenantLightDrainSystem))]
public sealed partial class FSRevenantLightDrainComponent : Component
{
    public readonly HashSet<EntityUid> Drained = new();
}
