using Content.Shared.RCD;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.RCD;

// The RCD's empty selection: no ghost, and a click builds nothing. Right-click on the ghost clears to it.
public static class FSRCDEmpty
{
    public static readonly ProtoId<RCDPrototype> Piece = "Invalid";
}
