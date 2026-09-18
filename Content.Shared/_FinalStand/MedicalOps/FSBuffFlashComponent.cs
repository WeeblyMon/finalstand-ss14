using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSBuffFlashComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Colour = Color.FromHex("#4FBF7A");

    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;
}
