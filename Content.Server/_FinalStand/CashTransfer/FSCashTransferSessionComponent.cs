namespace Content.Server._FinalStand.CashTransfer;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FSCashTransferSessionComponent : Component
{
    public EntityUid Target;
    public EntityUid Opener;
    [AutoPausedField] public TimeSpan CreatedAt;
}
