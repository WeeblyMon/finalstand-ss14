using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Upgrades;

// shot counter for OverchargeShot; every ShotsPerCycle fires a bolt instead of normal spread
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSOverchargeComponent : Component
{
    [DataField, AutoNetworkedField]
    public int ShotCounter = 0;

    public const int ShotsPerCycle = 3;
}
