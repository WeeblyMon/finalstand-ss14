using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps.Shop;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSSyringeUpgradeComponent : Component
{
    [AutoNetworkedField]
    public string? TierId;
}
