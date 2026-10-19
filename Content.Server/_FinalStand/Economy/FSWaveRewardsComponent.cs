using Robust.Shared.Network;
namespace Content.Server._FinalStand.Economy;

[RegisterComponent, Access(typeof(FSWaveRewardsSystem))]
public sealed partial class FSWaveRewardsComponent : Component
{
    // Used when an enemy has no FSEnemyValueComponent.
    [DataField]
    public int KillReward = 100;

    // The killer gets this share of the enemy's value; assisters get AssistShare of what the killer got.
    [DataField]
    public float KillShare = 0.5f;

    [DataField]
    public float AssistShare = 2f / 3f;

    [DataField]
    public int SecurityKillBonus = 50;

    [DataField]
    public int ClearBase = 550;

    [DataField]
    public int ClearPerWave = 150;

    [DataField]
    public float CreditsPerPrepSecondSkipped = 2f;

    [DataField]
    public int CatchUpPerWave = 1000;

    // Everything the whole crew has been paid so far, handed to late joiners on top of the catch-up.
    [ViewVariables]
    public int CrewPaidSoFar;

    // Keyed by player, not mind: leaving through cryo and rejoining makes a new mind.
    [ViewVariables]
    public readonly HashSet<NetUserId> CaughtUpUsers = new();
}

// Minds that damaged a wave enemy, for assist credit.
[RegisterComponent]
public sealed partial class FSEnemyDamageTrackingComponent : Component
{
    public readonly HashSet<EntityUid> AttackerMinds = new();
}
