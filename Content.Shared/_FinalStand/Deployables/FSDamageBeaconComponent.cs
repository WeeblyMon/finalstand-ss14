using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Deployables;

[RegisterComponent]
public sealed partial class FSDamageBeaconComponent : Component
{
    [DataField]
    public float Radius = 4f;

    [ViewVariables] public float? BaseRadius;

    [DataField]
    public float DamageMultiplier = 1.5f;

    [DataField]
    public EntProtoId? FieldVfxProtoId = "FSDamageBeaconFieldVfx";

    [DataField]
    public EntProtoId? DestroyVfxProtoId = "FSDamageBeaconDestroyVfx";
}
