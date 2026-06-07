using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.FriendlyFire;

/// <summary>
///     Marks a structure that should not take damage from wave players.
///     Enemies can still damage these structures normally.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FSPlayerDamageImmuneComponent : Component { }
