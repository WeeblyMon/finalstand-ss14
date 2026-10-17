namespace Content.Shared._FinalStand.Mech;

// A state prefix in broadsword_weapons.rsi for the gun drawn on the carrying mech.
[RegisterComponent]
public sealed partial class FSMechMountVisualComponent : Component
{
    [DataField(required: true)]
    public string State = string.Empty;
}
