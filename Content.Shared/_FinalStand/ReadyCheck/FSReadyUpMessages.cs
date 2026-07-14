using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.ReadyCheck;

[Serializable, NetSerializable]
public sealed class FSReadyUpRequestMessage : EntityEventArgs
{
    public bool IsReady;
    public FSReadyUpRequestMessage(bool isReady) => IsReady = isReady;
}

[Serializable, NetSerializable]
public sealed class FSReadyUpStateEvent : EntityEventArgs
{
    public int ReadyCount;
    public int TotalCount;
    public bool PlayerIsReady;

    public FSReadyUpStateEvent(int readyCount, int totalCount, bool playerIsReady)
    {
        ReadyCount = readyCount;
        TotalCount = totalCount;
        PlayerIsReady = playerIsReady;
    }
}
