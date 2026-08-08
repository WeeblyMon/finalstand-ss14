using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Mobs;

[DataDefinition]
public sealed partial class FSDeathLootEntry
{
    [DataField(required: true)]
    public EntProtoId Proto = default!;

    // Defaults low as a fail-safe for any future enemy that gets a drop without an explicit
    // YAML chance.
    [DataField]
    public float Chance = 0.0005f;
}

// Each entry rolls independently, so a mob can have a guaranteed drop and a rare one.
[RegisterComponent]
public sealed partial class FSDeathLootComponent : Component
{
    [DataField(required: true)]
    public List<FSDeathLootEntry> Drops = new();
}
