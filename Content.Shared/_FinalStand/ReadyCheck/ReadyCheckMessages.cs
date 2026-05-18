using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.ReadyCheck;

[Serializable, NetSerializable]
public sealed class ReadyCheckPDAUiState : BoundUserInterfaceState
{
    public ReadyStatus MyStatus;
    public Dictionary<string, ReadyStatus> AllStatuses;
    public bool IsCombatPhase;
    public bool IsCommandRole;  // false = show "Command access required"
    public bool IsCaptain;      // captain sees overview only, no ready button

    public ReadyCheckPDAUiState(
        ReadyStatus myStatus,
        Dictionary<string, ReadyStatus> allStatuses,
        bool isCombatPhase,
        bool isCommandRole,
        bool isCaptain)
    {
        MyStatus = myStatus;
        AllStatuses = allStatuses;
        IsCombatPhase = isCombatPhase;
        IsCommandRole = isCommandRole;
        IsCaptain = isCaptain;
    }
}

[Serializable, NetSerializable]
public sealed class ReadyCheckUiMessageEvent : CartridgeMessageEvent
{
    public ReadyStatus NewStatus;

    public ReadyCheckUiMessageEvent(ReadyStatus newStatus)
    {
        NewStatus = newStatus;
    }
}
