using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.FriendlyFire;

// Marks a player-controlled mob as a wave participant; FSFriendlyFireSharedSystem uses it to block player-on-player damage.
[RegisterComponent, NetworkedComponent]
public sealed partial class FSFriendlyFireComponent : Component { }
