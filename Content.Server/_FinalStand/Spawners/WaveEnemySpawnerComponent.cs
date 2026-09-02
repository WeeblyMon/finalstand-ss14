using Robust.Shared.GameObjects;

namespace Content.Server._FinalStand.Spawners;

[RegisterComponent]
public sealed partial class WaveEnemySpawnerComponent : Component
{
    [DataField]
    public int FromWave = 1;

    [DataField]
    public float SpawnRadius = 2.0f;

    [DataField]
    public string DirectionLabel = string.Empty;

    // Support spawners: rolled independently of the main selection and fed a smaller batch.
    [DataField]
    public bool Secondary;

    [DataField]
    public float ActivationChance = 0.4f;

    [DataField]
    public float BatchMultiplier = 0.5f;
}

[RegisterComponent]
public sealed partial class WaveSpawnedTagComponent : Component;
