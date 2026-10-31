using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(FSRevenantBoundSystem))]
public sealed partial class FSRevenantBoundComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;

    public bool Released;
}
