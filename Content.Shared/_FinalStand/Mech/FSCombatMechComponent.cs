// A mech built for the line: its pilot's side becomes the mech's side, and its hits pay a flat amount.
namespace Content.Shared._FinalStand.Mech;

[RegisterComponent]
public sealed partial class FSCombatMechComponent : Component
{
    [DataField]
    public int MoneyPerHit = 10;
}
