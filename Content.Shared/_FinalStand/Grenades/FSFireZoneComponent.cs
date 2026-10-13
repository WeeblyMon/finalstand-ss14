using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Grenades;

/// <summary>
/// Entity that persists at a location and ignites flammable entities in range.
/// Spawned by FSFireZoneOnTriggerSystem when an incendiary grenade detonates.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSFireZoneComponent : Component
{
    [DataField, AutoNetworkedField] public float Radius = 2.5f;
    [DataField] public float IgniteStacksPerSecond = 5f;
    [DataField] public EntProtoId FlameProtoId = "FSFireZoneFlame";

    public float Accumulator;
}
