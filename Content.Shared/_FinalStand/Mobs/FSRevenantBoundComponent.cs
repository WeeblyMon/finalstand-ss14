using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSRevenantBoundComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;

    public bool Released;
}
