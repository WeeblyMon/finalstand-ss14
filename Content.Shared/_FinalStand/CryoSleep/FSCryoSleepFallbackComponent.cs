using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.CryoSleep;

// Marks a cryopod usable as a wake-up point when the player's original pod is gone or occupied.
[RegisterComponent, NetworkedComponent]
public sealed partial class FSCryoSleepFallbackComponent : Component
{
}
