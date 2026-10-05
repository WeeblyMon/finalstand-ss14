// A mech built for the line: its pilot's side becomes the mech's side, its hits pay a flat amount, and some mount sizes are capped.
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Mech;

[RegisterComponent]
public sealed partial class FSCombatMechComponent : Component
{
    [DataField]
    public int MoneyPerHit = 10;

    [DataField]
    public Dictionary<ProtoId<TagPrototype>, int> MountLimits = new();

    public EntityUid? LastPilot;

    public bool WasBroken;
}
