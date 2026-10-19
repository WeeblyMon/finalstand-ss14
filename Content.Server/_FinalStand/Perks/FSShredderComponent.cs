namespace Content.Server._FinalStand.Perks;

// On a zombie; every stack raises the damage it takes from anyone.
[RegisterComponent]
[Access(typeof(FSPerkTargetDamageSystem))]
public sealed partial class FSShredderComponent : Component
{
    public int Stacks;
    public float PerStack;
    public TimeSpan ExpiresAt;
}
