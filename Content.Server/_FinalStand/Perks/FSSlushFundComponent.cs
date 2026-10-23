namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
[Access(typeof(FSSlushFundSystem))]
public sealed partial class FSSlushFundComponent : Component
{
    public float Remainder;
}
