using Robust.Shared.GameObjects;

namespace Content.Shared._FinalStand.Upgrades.Effects;

/// <summary>
/// Added to a weapon when an Execution Shot is primed (after reload).
/// Removed after the next shot fires.
/// </summary>
[RegisterComponent]
public sealed partial class FSExecutionReadyComponent : Component { }
