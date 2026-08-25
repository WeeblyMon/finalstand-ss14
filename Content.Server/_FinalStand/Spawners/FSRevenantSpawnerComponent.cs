namespace Content.Server._FinalStand.Spawners;

[RegisterComponent]
public sealed partial class FSRevenantSpawnerComponent : Component
{
    [DataField] public int FromWave = 15;

    [DataField] public float SpawnRadius = 1.5f;
}
