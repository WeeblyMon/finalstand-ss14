namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
[Access(typeof(FSManOnFireSystem), typeof(FSPerkBodySystem))]
public sealed partial class FSManOnFireComponent : Component
{
    public int Level;
}
