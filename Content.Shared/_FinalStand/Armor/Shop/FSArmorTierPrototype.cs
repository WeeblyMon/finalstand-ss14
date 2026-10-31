using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Armor.Shop;

// A hardsuit the armor shop sells. Its protection is read from the spawned item, not stored here.
[Prototype("fsArmorTier")]
public sealed partial class FSArmorTierPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public int Price;

    [DataField(required: true)]
    public EntProtoId Spawn;
}
