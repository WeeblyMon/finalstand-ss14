// Marks the centre of a cover zone (the emplacement) so clients can outline the tiles that give cover.
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Effects;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSCoverZoneComponent : Component
{
    [DataField]
    public int Radius = 1;
}
