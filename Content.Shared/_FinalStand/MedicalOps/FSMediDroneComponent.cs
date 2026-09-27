using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent]
public sealed partial class FSMediDroneComponent : Component
{
    [ViewVariables]
    public EntityUid? Owner;

    [ViewVariables]
    public EntityUid? Target;

    [DataField]
    public float DetectRange = 6f;

    [DataField]
    public float LeashRange = 9f;

    [DataField]
    public float FollowDistance = 1.2f;

    [DataField]
    public float HoverDistance = 1.3f;

    [DataField]
    public float Speed = 4.5f;

    [DataField]
    public TimeSpan ScanInterval = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan NextScan;

    [ViewVariables]
    public FSMediDroneCharge LastCharge = FSMediDroneCharge.Full;
}

[Serializable, NetSerializable]
public enum FSMediDroneVisuals : byte
{
    Charge,
}

[Serializable, NetSerializable]
public enum FSMediDroneCharge : byte
{
    Empty,
    Quarter,
    Half,
    ThreeQuarters,
    Full,
}
