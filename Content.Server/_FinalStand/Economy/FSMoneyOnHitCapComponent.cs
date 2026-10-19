namespace Content.Server._FinalStand.Economy;

[RegisterComponent]
[Access(typeof(FSMoneyOnHitSystem), typeof(Content.Server._FinalStand.Weapons.FSHarvesterWeaponSystem))]
public sealed partial class FSMoneyOnHitCapComponent : Component
{
    [DataField] public int MaxMoneyPerPlayer = 200;
    public Dictionary<EntityUid, int> MoneyGivenPerPlayer = new();
    public Dictionary<EntityUid, int> ResearchGivenPerPlayer = new();
}
