namespace Content.Server._FinalStand.Weapons;

[RegisterComponent]
public sealed partial class FSResearchOnHitComponent : Component
{
    [DataField]
    public int ResearchPerHit = 30;
}
