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

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(WaveGameRuleSystem), typeof(WaveEnemySpawningSystem), typeof(WaveEnemyScalingSystem))]
public sealed partial class WaveGameRuleComponent : Component
{
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
    public List<int> MaxLanesByPlayers = new() { 2, 2, 3, 3 };

    [DataField]
    public int BossWavePerkReward = 1;

    [DataField]
    public List<EntProtoId> BossPool = new() { "FSZombieGiant" };

    [DataField]
    public List<SpecialEnemyConfig> SpecialEnemyPool = new();

    [DataField]
    public LocId FactionDisplay = "fs-ccc-faction-unknown";

    [DataField]
    public SoundSpecifier? WaveStartSound = new SoundPathSpecifier("/Audio/_FinalStand/WaveEvents/wave_start.ogg");

    [DataField]
    public SoundSpecifier? WaveEndSound = new SoundPathSpecifier("/Audio/_FinalStand/WaveEvents/wave_end.ogg");

    [DataField]
    public SoundSpecifier? WaveVoteCountdownSound = new SoundPathSpecifier("/Audio/_FinalStand/WaveEvents/vote_countdown.ogg");

    // Set to true when a majority vote has triggered the 10-second countdown; reset each prep phase.
    public bool VoteCountdownActive = false;
    public bool VoteCountdownSoundPlayed = false;
    [AutoPausedField]
    public TimeSpan VoteCountdownSoundTime = TimeSpan.Zero;

    // Runtime state

    public int WaveNumber = 1;
    public WavePhase Phase = WavePhase.Prep;
    [AutoPausedField]
    public TimeSpan PhaseEndTime = TimeSpan.Zero;
    [AutoPausedField]
    public TimeSpan CombatStartTime = TimeSpan.Zero;
    [AutoPausedField]
    public TimeSpan NextSpawnTime = TimeSpan.Zero;
    public int EnemyTotalThisWave = 0;
    public int EnemiesSpawnedThisWave = 0;
    public int WavesCompleted = 0;
    public int TotalEnemiesKilled = 0;

    public readonly HashSet<EntityUid> AliveEnemies = new();
    public readonly List<EntityUid> SpawnerEntities = new();
    public readonly HashSet<EntityUid> PreviousSpawnerEntities = new();
    public EntityUid CCCEntity = EntityUid.Invalid;
    [AutoPausedField]
    public TimeSpan NextHeartbeatTime = TimeSpan.Zero;
    [AutoPausedField]
    public TimeSpan NextSpawnerRetryTime = TimeSpan.Zero;

    public int ScalingPlayersThisWave = 0;

    [DataField]
    public float BaseZombieMeleeDamage = 10f;

    public bool SpawnPaused = false;

    public EntityUid GiantEntity = EntityUid.Invalid;

    // The wave whose boss perk point has been paid. A wave replayed after a lost CCC life spawns its boss again but
    // doesn't pay the point again.
    public int BossRewardPaidWave;

    // Clearing this wave wins the round; 0 turns victory off.
    [DataField]
    public int VictoryWave = 30;

    [DataField]
    public int VictoryPerkReward = 5;

    // Endless adds a modifier every this many waves past VictoryWave.
    [DataField]
    public int EndlessEscalationInterval = 5;

    [DataField]
    public bool DifficultyVote = true;

    public bool Victory;
    public bool AwaitingEndless;
    public bool Endless;

    // An admin skipped waves, so the run doesn't count for the ladder.
    public bool AdminAssisted;

    public FSDifficulty Difficulty = FSDifficulty.Normal;

    // Difficulty and modifiers are fixed once wave 1 starts.
    public bool DifficultyLocked;

    public readonly List<ProtoId<FSRoundModifierPrototype>> Modifiers = new();
    public bool ModifiersRerolled;

    public ProtoId<FSWeeklyChallengePrototype>? WeeklyChallenge;
    public string? WeeklyKey;

    public bool RoundLost;
}
