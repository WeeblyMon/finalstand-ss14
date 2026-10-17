using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Mech;

// The pilot's side becomes the mech's side, hits pay a flat amount, and some mount sizes are capped.
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
