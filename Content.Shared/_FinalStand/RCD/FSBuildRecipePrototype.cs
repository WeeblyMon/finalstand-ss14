using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.RCD;

// What an RCD-built structure costs. The ID matches the RCD prototype and the entity it spawns.
[Prototype("fsBuildRecipe")]
public sealed partial class FSBuildRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Dictionary<ProtoId<StackPrototype>, int> Materials = new();

    [DataField]
    public string? CapGroup;

    [DataField]
    public string? Research;

    [DataField]
    public SoundSpecifier? LandSound;

    [DataField]
    public bool Station;
}
