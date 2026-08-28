namespace Content.Server._FinalStand.Economy;

[RegisterComponent]
public sealed partial class FSMoneyOnHitCapComponent : Component
{
    [DataField] public int MaxMoneyPerPlayer = 200;
    public Dictionary<EntityUid, int> MoneyGivenPerPlayer = new();
}
