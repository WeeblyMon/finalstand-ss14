namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
[Access(typeof(FSPackMuleSystem), typeof(FSPerkBodySystem))]
public sealed partial class FSPackMuleComponent : Component
{
    public int Level;
}
