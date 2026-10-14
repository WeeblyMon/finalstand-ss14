namespace Content.Shared._FinalStand.Weapons;

// Marker identifying the Harvester gun entity and its beam.
[RegisterComponent]
public sealed partial class FSHarvesterComponent : Component
{
    [DataField] public float Damage = 2f;
}
