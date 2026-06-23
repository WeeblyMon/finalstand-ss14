// Wielder-side timer for the Pirate cross-family crit window.
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Upgrades.Effects;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSFlintlockCritWindowComponent : Component
{
    [DataField, AutoNetworkedField] public TimeSpan ExpiresAt = TimeSpan.Zero;
}
