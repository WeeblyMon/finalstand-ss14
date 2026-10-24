using Content.Shared.Eui;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Admin;

[Serializable, NetSerializable]
public sealed class FSZombieFieldInfo
{
    public string Component = string.Empty;
    public string Field = string.Empty;
    public float Value;
    public bool IsInt;
}

[Serializable, NetSerializable]
public sealed class FSZombieFieldOverride
{
    public string Component = string.Empty;
    public string Field = string.Empty;
    public float Value;
}

public static class FSCustomZombieEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class RequestFields : EuiMessageBase
    {
        public string ProtoId = string.Empty;
    }

    [Serializable, NetSerializable]
    public sealed class FieldData : EuiMessageBase
    {
        public string ProtoId = string.Empty;
        public float Health;
        public float WalkSpeed;
        public float SprintSpeed;
        public List<FSZombieFieldInfo> Fields = new();
    }

    [Serializable, NetSerializable]
    public sealed class SpawnRequest : EuiMessageBase
    {
        public string ProtoId = string.Empty;
        public NetCoordinates Coordinates;
        public float Health;
        public float DamageMultiplier = 1f;
        public float WalkSpeed;
        public float SprintSpeed;
        public float Scale = 1f;
        public float DamageTakenMultiplier = 1f;
        public List<FSZombieFieldOverride> Overrides = new();
    }
}
