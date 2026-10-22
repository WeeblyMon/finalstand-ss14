using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Melee;

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

    [AutoNetworkedField]
    public TimeSpan NextSprintAttack;
}

// Rev built up by grinding one target.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMeleeRevComponent : Component
{
    // Not networked: the target can be deleted mid-grind.
    public EntityUid? Target;

    [AutoNetworkedField]
    public int Stacks;

    [AutoNetworkedField]
    public TimeSpan Last;
}

// Knocked hard enough to be executed while badly hurt.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMeleeStaggeredComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public TimeSpan Until;
}

// Kills on the weapon, shown as blood on its sprite until the wave ends.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSMeleeBloodComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public int Kills;

    public static readonly int[] StageKills = [1, 4, 8];

    public int Stage
    {
        get
        {
            var stage = 0;
            while (stage < StageKills.Length && Kills >= StageKills[stage])
            {
                stage++;
            }

            return stage;
        }
    }
}
