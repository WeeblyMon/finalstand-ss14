using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.CashTransfer;

[Serializable, NetSerializable]
public enum FSCashTransferUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FSCashTransferBuiState : BoundUserInterfaceState
{
    public readonly string TargetName;
    public readonly int SenderBalance;

    public FSCashTransferBuiState(string targetName, int senderBalance)
    {
        TargetName = targetName;
        SenderBalance = senderBalance;
    }
}

[Serializable, NetSerializable]
public sealed class FSCashTransferRequestMessage : BoundUserInterfaceMessage
{
    public readonly int Amount;

    public FSCashTransferRequestMessage(int amount)
    {
        Amount = amount;
    }
}
