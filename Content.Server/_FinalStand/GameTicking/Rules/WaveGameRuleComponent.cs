using Content.Shared._FinalStand.GameTicking;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.GameTicking.Rules;

[DataDefinition]
public sealed partial class SpecialEnemyConfig
{
    [DataField]
    public EntProtoId EnemyId = "FSZombieBloater";

    [DataField]
    public int FromWave = 7;

    // Sequential independent roll: each special checked in list order; first hit wins.
    [DataField]
    public float SpawnChance = 0.25f;
}

[DataDefinition]
public sealed partial class WaveEnemyConfig
{
    [DataField]
    public int FromWave = 1;
    [DataField]
    public int? ToWave = null;

    [DataField]
    public List<EntProtoId> EnemyPool = new() { "MobXeno" };
}

[RegisterComponent, Access(typeof(WaveGameRuleSystem), typeof(WaveEnemySpawningSystem), typeof(WaveEnemyScalingSystem))]
public sealed partial class WaveGameRuleComponent : Component
{
    // adjustable timers

    [DataField]
    public TimeSpan PrepDuration = TimeSpan.FromSeconds(600);

    [DataField]
    public TimeSpan MaxCombatDuration = TimeSpan.FromSeconds(1800);

    [DataField]
    public float MinSpawnInterval = 0.2f;

    [DataField]
    public float MaxSpawnInterval = 0.5f;

    [DataField]
    public int SpawnBatchSize = 2;

    [DataField]
    public List<WaveEnemyConfig> EnemyConfigs = new()
    {
        new WaveEnemyConfig { FromWave = 1, EnemyPool = new List<EntProtoId> { "MobXeno" } },
    };

    [DataField]
    public int MaxEnemyCap = 130;

    [DataField]
    public int PlayerEnemyBonus = 9;

    [DataField]
    public int PlayerBonusFromWave = 1;

    [DataField]
    public int KillReward = 100;

    [DataField]
    public int SecKillBonus = 50;

    [DataField]
    public int BossWavePerkReward = 1;

    [DataField]
    public List<EntProtoId> BossPool = new() { "FSZombieGiant" };

    [DataField]
    public List<SpecialEnemyConfig> SpecialEnemyPool = new();

    [DataField]
    public string FactionDisplay = "Unknown hostiles detected";

    [DataField]
    public SoundSpecifier? WaveStartSound = new SoundPathSpecifier("/Audio/_FinalStand/WaveEvents/wave_start.ogg");

    [DataField]
    public SoundSpecifier? WaveEndSound = new SoundPathSpecifier("/Audio/_FinalStand/WaveEvents/wave_end.ogg");

    [DataField]
    public SoundSpecifier? WaveVoteCountdownSound = new SoundPathSpecifier("/Audio/_FinalStand/WaveEvents/vote_countdown.ogg");

    // Set to true when a majority vote has triggered the 10-second countdown; reset each prep phase.
    public bool VoteCountdownActive = false;
    public bool VoteCountdownSoundPlayed = false;
    public TimeSpan VoteCountdownSoundTime = TimeSpan.Zero;

    // ── Runtime state

    public int WaveNumber = 1;
    public WavePhase Phase = WavePhase.Prep;
    public TimeSpan PhaseEndTime = TimeSpan.Zero;
    public TimeSpan NextSpawnTime = TimeSpan.Zero;
    public int EnemyTotalThisWave = 0;
    public int EnemiesSpawnedThisWave = 0;
    public int WavesCompleted = 0;
    public int TotalEnemiesKilled = 0;

    public readonly HashSet<EntityUid> AliveEnemies = new();
    public readonly List<EntityUid> SpawnerEntities = new();
    public EntityUid CCCEntity = EntityUid.Invalid;
    public TimeSpan NextHeartbeatTime = TimeSpan.Zero;
    public TimeSpan NextTimerBroadcastTime = TimeSpan.Zero;
    public TimeSpan NextEnemyCountBroadcast = TimeSpan.Zero;
    public TimeSpan NextSpawnerRetryTime = TimeSpan.Zero;

    public int PlayersThisWave = 0;
    public readonly HashSet<EntityUid> PaidCatchUpMinds = new();

    [DataField]
    public float BaseZombieMeleeDamage = 10f;

    public bool SpawnPaused = false;

    public EntityUid GiantEntity = EntityUid.Invalid;
    public bool GiantApAwarded = false;

    public int AccumulatedSurvivalBonus = 0;
}

[RegisterComponent]
public sealed partial class FSEnemyDamageTrackingComponent : Component
{
    public readonly HashSet<EntityUid> AttackerMinds = new();
}
