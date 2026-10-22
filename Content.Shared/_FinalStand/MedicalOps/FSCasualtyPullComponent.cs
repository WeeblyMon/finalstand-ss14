using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FSCasualtyPullComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ReadyAt;
}
