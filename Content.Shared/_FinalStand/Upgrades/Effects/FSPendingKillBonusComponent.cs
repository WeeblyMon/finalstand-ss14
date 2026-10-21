namespace Content.Shared._FinalStand.Upgrades.Effects;

[RegisterComponent]
public sealed partial class FSPendingKillBonusComponent : Component
{
    [DataField] public int MoneyBonus = 0;
    [DataField] public float MoneyPercent = 0f;
}
