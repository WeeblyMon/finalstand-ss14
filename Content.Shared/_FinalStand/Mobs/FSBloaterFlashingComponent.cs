using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mobs;

/// <summary>
/// Added to a Bloater when it enters Dead state. Client system reads this to play the red flash animation.
/// Removed by server after the flash duration elapses (immediately before the explosion fires).
/// </summary>
[NetworkedComponent, RegisterComponent]
public sealed partial class FSBloaterFlashingComponent : Component { }
