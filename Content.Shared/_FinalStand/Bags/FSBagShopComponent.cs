using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Bags;

// Vendor that dispenses storage bags onto the floor. Catalog lives in YAML so prices stay tunable.
[RegisterComponent]
public sealed partial class FSBagShopComponent : Component
{
    [DataField]
    public List<FSBagShopEntry> Catalog = new();
}

[DataDefinition]
public sealed partial class FSBagShopEntry
{
    [DataField(required: true)]
    public EntProtoId Proto;

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField]
    public string Description = string.Empty;

    [DataField]
    public int Price;
}
