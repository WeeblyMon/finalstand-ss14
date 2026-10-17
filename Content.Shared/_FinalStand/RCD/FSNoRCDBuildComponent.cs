using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.RCD;

// Map marker. Anchored on a tile, it stops the RCD building anything there except floor tiles.
[RegisterComponent, NetworkedComponent]
public sealed partial class FSNoRCDBuildComponent : Component;
