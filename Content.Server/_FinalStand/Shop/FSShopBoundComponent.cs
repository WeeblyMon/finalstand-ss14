namespace Content.Server._FinalStand.Shop;

// Limited shop items belong to whoever bought them, so dropping one can't be used to buy another.
[RegisterComponent]
public sealed partial class FSShopBoundComponent : Component
{
    public EntityUid OwnerMind;
}
