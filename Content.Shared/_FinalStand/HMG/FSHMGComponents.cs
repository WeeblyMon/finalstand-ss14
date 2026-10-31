using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.HMG;

// A crewed heavy machine gun: the gun lives on this entity and fires for whoever is buckled to it.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(FSHMGSystem))]
public sealed partial class FSHMGComponent : Component
{
    [AutoNetworkedField]
    public EntityUid? Gunner;

    [AutoNetworkedField]
    public EntityUid? Crate;

    [DataField]
    public float Arc = 90f;

    [AutoNetworkedField]
    public float ArcBonus;

    [AutoNetworkedField]
    public float HeatMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float DamageMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float FireRateMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float UpgradeHeatMultiplier = 1f;

    // Sustained fire overheats in about 4 s; an overheated gun is locked until it has fully cooled.
    [DataField]
    public float HeatPerShot = 0.075f;

    [DataField]
    public float CoolPerSecond = 0.34f;

    [DataField, AutoNetworkedField]
    public float Heat;

    [AutoNetworkedField, AutoPausedField]
    public TimeSpan HeatAt;

    [AutoNetworkedField, AutoPausedField]
    public TimeSpan OverheatedUntil;

    [DataField]
    public float LinkRange = 1.5f;

    [DataField]
    public float LookPvsIncrease = 0.6f;

    public float LinkBonus;

    [DataField]
    public TimeSpan MessageCooldown = TimeSpan.FromSeconds(1);

    [AutoPausedField] public TimeSpan NextMessage;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(FSHMGSystem))]
public sealed partial class FSHMGGunnerComponent : Component
{
    [AutoNetworkedField]
    public EntityUid Hmg;

    public bool WasInCombatMode;

    [AutoNetworkedField]
    public bool ShowArc = true;

    [AutoNetworkedField]
    public EntityUid? ArcAction;
}

public sealed partial class FSToggleHMGArcActionEvent : InstantActionEvent;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSAmmoCrateComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Rounds = 3000;

    [DataField]
    public int MaxRounds = 3000;

    public bool Compressed;

    [AutoNetworkedField]
    public EntityUid? LinkedTo;
}

// Projectiles that pay a fixed amount per hit no matter what the shooter is holding.
[RegisterComponent]
public sealed partial class FSFlatMoneyProjectileComponent : Component
{
    [DataField]
    public int Amount = 10;
}

[Robust.Shared.Serialization.NetSerializable, Serializable]
public enum FSAmmoCrateVisuals : byte
{
    Level,
}

[Robust.Shared.Serialization.NetSerializable, Serializable]
public enum FSAmmoCrateLevel : byte
{
    Empty,
    Low,
    Medium,
    High,
    Full,
}
