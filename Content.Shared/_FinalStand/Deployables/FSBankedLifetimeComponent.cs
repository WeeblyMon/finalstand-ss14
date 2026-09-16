using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Deployables;

// Lifetime carried across a pack-up, so folding and re-pitching spends the same budget rather than
// resetting it. Without this the tent is permanent.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSBankedLifetimeComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Remaining;
}
