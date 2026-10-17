using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._FinalStand.Fortifications;

// A grade is a material tier, a branch a positional role; both are poured in with the Engineer's Hammer.
public enum FSUpgradeSlot : byte
{
    Grade,
    Branch,
    Module,
}

[Prototype("fsUpgrade")]
public sealed partial class FSUpgradePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public FSUpgradeSlot Slot;

    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;

    [DataField]
    public Dictionary<ProtoId<StackPrototype>, int> Cost = new();

    [DataField]
    public int Hits = 5;

    // For a grade this is relative to steel; for a branch it multiplies on top of the grade.
    [DataField]
    public float Health = 1f;

    [DataField]
    public float Breach = 1f;

    [DataField]
    public float RepairDelay = 1f;

    [DataField]
    public ProtoId<FSUpgradePrototype>? Requires;

    [DataField]
    public ProtoId<StackPrototype>? PlateMaterial;

    [DataField]
    public string? State;

    [DataField]
    public string? Cracks;

    [DataField]
    public string? FoldingState;

    [DataField]
    public string? FoldedState;

    [DataField]
    public string? FoldingCracks;

    [DataField]
    public string? FoldedCracks;

    [DataField]
    public Color? Color;

    [DataField]
    public Color? HudColor;

    [DataField]
    public List<PrototypeLayerData> Layers = new();

    [DataField]
    public ComponentRegistry? Components;

    [DataField]
    public FSSentryModifier? Sentry;

    [DataField]
    public FSHMGModifier? Hmg;
}

[DataDefinition]
public sealed partial class FSHMGModifier
{
    [DataField]
    public float Damage;

    [DataField]
    public float FireRate = 1f;

    [DataField]
    public float Heat = 1f;
}

[DataDefinition]
public sealed partial class FSSentryModifier
{
    [DataField]
    public int Ammo;

    [DataField]
    public float FireInterval = 1f;

    [DataField]
    public float Damage;

    [DataField]
    public float Range;
}
