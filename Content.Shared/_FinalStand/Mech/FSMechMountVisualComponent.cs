// Which drawn gun a mech weapon shows on the mech that carries it (a state prefix in broadsword_weapons.rsi).
namespace Content.Shared._FinalStand.Mech;

[RegisterComponent]
public sealed partial class FSMechMountVisualComponent : Component
{
    [DataField(required: true)]
    public string State = string.Empty;
}
