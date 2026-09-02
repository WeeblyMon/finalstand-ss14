using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Deployables;

[Serializable, NetSerializable]
public enum FSSentryTurretVisuals : byte
{
    Angle,
    Firing,
}

[Serializable, NetSerializable]
public enum FSSentryTurretLayers : byte
{
    Base,
    Gun,
}
