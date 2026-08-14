using Content.Shared._FinalStand.Sprint;

namespace Content.Client._FinalStand.Sprint;

// Exists purely to instantiate SharedFSSprintSystem client-side, so its key binding and
// RefreshMovementSpeedModifiersEvent handler actually run locally for client-side prediction.
public sealed class FSSprintClientSystem : SharedFSSprintSystem
{
}
