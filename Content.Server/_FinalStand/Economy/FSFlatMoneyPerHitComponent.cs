namespace Content.Server._FinalStand.Economy;

// Pays a fixed amount per hit and ignores held-item bonuses, for support weapons that aren't meant as income.
[RegisterComponent]
public sealed partial class FSFlatMoneyPerHitComponent : Component
{
    [DataField]
    public int Amount = 10;
}
