namespace Content.Shared._FinalStand.Weapons;

[RegisterComponent]
[Access(typeof(FSXrayRaycastSystem))]
public sealed partial class FSXrayRaycastComponent : Component
{
    [DataField] public float MaxDistance = 20f;
}
