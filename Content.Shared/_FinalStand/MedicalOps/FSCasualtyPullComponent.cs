using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSCasualtyPullComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan ReadyAt;
}
