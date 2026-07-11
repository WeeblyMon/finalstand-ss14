namespace Content.Shared._FinalStand.Weapons;

[RegisterComponent]
public sealed partial class FSXrayRaycastComponent : Component
{
    [DataField] public float MaxDistance = 20f;
}
