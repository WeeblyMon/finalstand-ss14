// Shredder vulnerability on a zombie: every stack raises the damage it takes from anyone.
namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
public sealed partial class FSShredderComponent : Component
{
    public int Stacks;
    public float PerStack;
    public TimeSpan ExpiresAt;
}
