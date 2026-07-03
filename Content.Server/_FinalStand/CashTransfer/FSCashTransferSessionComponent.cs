using Robust.Shared.GameObjects;

namespace Content.Server._FinalStand.CashTransfer;

[RegisterComponent]
public sealed partial class FSCashTransferSessionComponent : Component
{
    public EntityUid Target;
}
