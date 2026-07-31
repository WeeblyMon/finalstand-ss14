using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Mobs;

// Chance to spawn LootProtoId on death - Chance defaults low as a fail-safe for any future
// enemy that gets this component without an explicit YAML override.
[RegisterComponent]
public sealed partial class FSDeathLootComponent : Component
{
    [DataField(required: true)]
    public EntProtoId LootProtoId = default!;

    [DataField]
    public float Chance = 0.0005f;
}
