using Content.Goobstation.Common.Barks;
using Content.Shared.DisplacementMap;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Robust.Shared.Enums;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Humanoid;

[NetworkedComponent, RegisterComponent, AutoGenerateComponentState(true)]
public sealed partial class HumanoidAppearanceComponent : Component
{
    public MarkingSet ClientOldMarkings = new();

    [DataField, AutoNetworkedField]
    public MarkingSet MarkingSet = new();

    [DataField]
    public Dictionary<HumanoidVisualLayers, HumanoidSpeciesSpriteLayer> BaseLayers = new();

    [DataField, AutoNetworkedField]
    public HashSet<HumanoidVisualLayers> PermanentlyHidden = new();

    [DataField, AutoNetworkedField]
    public Gender Gender;

    [DataField, AutoNetworkedField]
    public int Age = 18;

    [DataField]
    public ProtoId<BarkPrototype> BarkVoice { get; set; } = "Alto";

    [DataField, AutoNetworkedField]
    public Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> CustomBaseLayers = new();

    [DataField(required: true), AutoNetworkedField]
    public ProtoId<SpeciesPrototype> Species { get; set; }

    [DataField]
    public ProtoId<HumanoidProfilePrototype>? Initial { get; private set; }

    [DataField, AutoNetworkedField]
    public Color SkinColor { get; set; } = Color.FromHex("#C0967F");

    [DataField, AutoNetworkedField]
    public Dictionary<HumanoidVisualLayers, SlotFlags> HiddenLayers = new();

    [DataField, AutoNetworkedField]
    public Sex Sex = Sex.Male;

    [DataField, AutoNetworkedField]
    public Color EyeColor = Color.Brown;

    [ViewVariables(VVAccess.ReadOnly)]
    public Color? CachedHairColor;

    [ViewVariables(VVAccess.ReadOnly)]
    public Color? CachedFacialHairColor;

    [DataField]
    public HashSet<HumanoidVisualLayers> HideLayersOnEquip = [HumanoidVisualLayers.Hair];

    [DataField]
    public Dictionary<HumanoidVisualLayers, DisplacementData> MarkingsDisplacement = new();

    /// <summary>
    ///     Shitmed Change: Used to prevent early additions of BodyPartAppearanceComp with incorrect data from Urists.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ProfileLoaded;

    [DataField, AutoNetworkedField]
    public float Height = 1f;

    [DataField, AutoNetworkedField]
    public float Width = 1f;
}

[DataDefinition]
[Serializable, NetSerializable]
public readonly partial struct CustomBaseLayerInfo
{
    public CustomBaseLayerInfo(string? id, Color? color = null, string? shader = null)
    {
        DebugTools.Assert(id == null || IoCManager.Resolve<IPrototypeManager>().HasIndex<HumanoidSpeciesSpriteLayer>(id));
        Id = id;
        Color = color;
        Shader = shader;
    }

    [DataField]
    public ProtoId<HumanoidSpeciesSpriteLayer>? Id { get; init; }

    [DataField]
    public Color? Color { get; init; }

    [DataField]
    public string? Shader { get; init; }
}
