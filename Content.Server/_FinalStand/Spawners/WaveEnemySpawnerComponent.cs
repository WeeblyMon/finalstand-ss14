using Robust.Shared.GameObjects;

namespace Content.Server._FinalStand.Spawners;

[RegisterComponent]
public sealed partial class WaveEnemySpawnerComponent : Component
{
    [DataField]
    public int FromWave = 1;

    [DataField]
    public float SpawnRadius = 2.0f;
}

[RegisterComponent]
public sealed partial class WaveSpawnedTagComponent : Component;
