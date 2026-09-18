using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSSurgeryProgressComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<string> Started = new();
}
