using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Structures;

// sprite layer keys for barricade damage stages; member names must match the RSI state prefixes
[Serializable, NetSerializable]
public enum FSDamageOverlayVisuals : byte
{
    DamageOverlay,
    AdditionalDamageOverlay,
}
