namespace Content.Server._FinalStand.Weapons;

[RegisterComponent]
[Access(typeof(FSHarvesterWeaponSystem))]
public sealed partial class FSResearchOnHitComponent : Component
{
    [DataField]
    public int ResearchPerHit = 30;
}
