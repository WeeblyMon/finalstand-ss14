using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.CryoSleep;

// leaving cryosleep mid-round, ported from New Frontier via Monolith
[Serializable, NetSerializable]
public enum FSReturnToBodyStatus : byte
{
    Success,
    BodyMissing,
    NoCryopodAvailable,
    NotAGhost,
    Disabled,
}

[Serializable, NetSerializable]
public sealed class FSCryoWakeupRequestEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class FSCryoWakeupResponseEvent : EntityEventArgs
{
    public readonly FSReturnToBodyStatus Status;

    public FSCryoWakeupResponseEvent(FSReturnToBodyStatus status)
    {
        Status = status;
    }
}

[Serializable, NetSerializable]
public sealed class FSCryoReturnToLobbyEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class FSCryoStatusEvent : EntityEventArgs
{
    public readonly bool HasStoredBody;

    public FSCryoStatusEvent(bool hasStoredBody)
    {
        HasStoredBody = hasStoredBody;
    }
}
