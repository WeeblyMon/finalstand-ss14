using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.FriendlyFire;

/// <summary>
///     Marks a player-controlled mob as a wave participant.
///     Used by FSFriendlyFireSharedSystem to block all player-on-player damage.
///     Added by FSFriendlyFireSystem when a WaveGameRule is active.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FSFriendlyFireComponent : Component { }
