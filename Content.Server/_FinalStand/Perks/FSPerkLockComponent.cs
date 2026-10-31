namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
[Access(typeof(FSPerkLockSystem))]
public sealed partial class FSPerkLockComponent : Component
{
    public HashSet<string> Locked = new();
}
