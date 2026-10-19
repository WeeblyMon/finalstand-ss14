namespace Content.Shared._FinalStand.Upgrades.Effects;

// Left on a wave enemy by a weapon with a per-kill money upgrade; pays that player if they finish it.
[RegisterComponent]
public sealed partial class FSPendingKillBonusComponent : Component
{
    [DataField] public int MoneyBonus = 0;

    [ViewVariables]
    public EntityUid? Payee;
}
