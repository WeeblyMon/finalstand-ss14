using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Deployables;

// On the placed Damage Beacon - see FSDamageBeaconSystem.
[RegisterComponent]
public sealed partial class FSDamageBeaconComponent : Component
{
    [DataField]
    public float Radius = 4f;

    [DataField]
    public float DamageMultiplier = 1.5f;

    [DataField]
    public EntProtoId? FieldVfxProtoId = "FSDamageBeaconFieldVfx";

    [DataField]
    public EntProtoId? DestroyVfxProtoId = "FSDamageBeaconDestroyVfx";
}
