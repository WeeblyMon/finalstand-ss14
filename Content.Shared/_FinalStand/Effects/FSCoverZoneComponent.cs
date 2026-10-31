using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Effects;

// On the emplacement at the centre of a cover zone, so clients can outline the tiles that give cover.
[RegisterComponent, NetworkedComponent]
public sealed partial class FSCoverZoneComponent : Component
{
    [DataField]
    public int Radius = 1;
}
