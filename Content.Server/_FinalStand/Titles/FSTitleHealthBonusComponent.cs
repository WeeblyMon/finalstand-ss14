namespace Content.Server._FinalStand.Titles;

[RegisterComponent, Access(typeof(FSTitleBuffSystem))]
public sealed partial class FSTitleHealthBonusComponent : Component
{
    [DataField]
    public float Applied;
}
