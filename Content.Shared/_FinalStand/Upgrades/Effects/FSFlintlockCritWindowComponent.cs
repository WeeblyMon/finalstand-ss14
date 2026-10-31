using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Upgrades.Effects;

// Wielder-side timer for the Pirate cross-family crit window.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FSFlintlockCritWindowComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField] public TimeSpan ExpiresAt = TimeSpan.Zero;
}
