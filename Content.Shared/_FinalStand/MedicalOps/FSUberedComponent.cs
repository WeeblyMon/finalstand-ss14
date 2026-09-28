using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSUberedComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;

    [DataField, AutoNetworkedField]
    public Color SourceColor = Color.FromHex("#FF6A3D");

    [DataField, AutoNetworkedField]
    public bool InfiniteStamina;

    [ViewVariables]
    public TimeSpan NextTing;
}
