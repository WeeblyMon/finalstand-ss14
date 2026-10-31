namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
[Access(typeof(FSDrunkPerkSystem))]
public sealed partial class FSDrunkPerkComponent : Component
{
    public bool WasDrunk;
}
