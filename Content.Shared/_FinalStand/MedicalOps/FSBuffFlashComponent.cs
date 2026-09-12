using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

// Short world-space tell that a buff just landed, so bystanders read a dart or flask as help.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSBuffFlashComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Colour = Color.FromHex("#4FBF7A");

    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;
}
