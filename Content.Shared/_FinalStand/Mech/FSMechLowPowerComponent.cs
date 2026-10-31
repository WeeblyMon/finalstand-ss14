using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mech;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMechLowPowerComponent : Component
{
    [DataField]
    public float Fraction = 0.05f;

    [DataField]
    public float SpeedModifier = 0.65f;

    [DataField]
    public SoundSpecifier Alarm = new SoundPathSpecifier("/Audio/Machines/warning_buzzer.ogg");

    [AutoNetworkedField]
    public bool Active;
}
