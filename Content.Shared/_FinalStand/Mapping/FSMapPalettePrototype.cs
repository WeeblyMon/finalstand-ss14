using Content.Shared.Decals;
using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Mapping;

[Prototype("fsMapPalette")]
public sealed partial class FSMapPalettePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public LocId? Name;

    [DataField]
    public int Order;

    [DataField]
    public bool Root;

    [DataField]
    public List<ProtoId<FSMapPalettePrototype>> Children = new();

    [DataField]
    public List<ProtoId<ContentTileDefinition>> Tiles = new();

    [DataField]
    public List<EntProtoId> Entities = new();

    [DataField]
    public List<ProtoId<DecalPrototype>> Decals = new();
}
