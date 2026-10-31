using System.Numerics;

namespace Content.Shared._FinalStand.Ailments;

[RegisterComponent]
public sealed partial class FSRecoveryZoneComponent : Component
{
    [DataField]
    public float Radius = 2f;

    [DataField]
    public Vector2 Offset;

    [DataField]
    public float CureSeconds = 8f;

    [DataField]
    public TimeSpan RegenDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public float RegenPerSecond = 1f;

    [DataField]
    public float RegenCap = 0.7f;
}
