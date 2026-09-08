using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSCasualtyStatusComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? Responder;
}
