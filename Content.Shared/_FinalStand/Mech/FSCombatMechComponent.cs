// A mech built for the line: its pilot's side becomes the mech's side, and its mounted guns only fire from inside it.
using System.Numerics;

namespace Content.Shared._FinalStand.Mech;

[RegisterComponent]
public sealed partial class FSCombatMechComponent : Component
{
    [DataField]
    public Vector2 PilotZoom = new(1.3f, 1.3f);

    [DataField]
    public int MoneyPerHit = 10;
}
