using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Melee;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMeleeSwingComponent : Component
{
    [AutoNetworkedField]
    public TimeSpan AnticipationEnd;

    [AutoNetworkedField]
    public TimeSpan ImpactEnd;

    [AutoNetworkedField]
    public TimeSpan RecoveryEnd;

    [AutoNetworkedField]
    public float MoveAnticipation = 1f;

    [AutoNetworkedField]
    public float MoveImpact = 1f;

    [AutoNetworkedField]
    public float MoveRecovery = 1f;

    public float AppliedMultiplier = 1f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMeleeFlinchComponent : Component
{
    [AutoNetworkedField]
    public TimeSpan EndTime;

    [AutoNetworkedField]
    public float Speed = 1f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMeleeComboComponent : Component
{
    [AutoNetworkedField]
    public EntityUid? Weapon;

    [AutoNetworkedField]
    public int Hits;

    [AutoNetworkedField]
    public TimeSpan Expire;
}
