using System.Numerics;
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

    // Bloodthirsty: a rev dump that killed makes the next grind hit crit.
    [AutoNetworkedField]
    public bool CritReady;
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

// The sweeper's chain: stacks built by sweeping in quick succession, and who they've passed through lately.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMeleeSweepComponent : Component
{
    [AutoNetworkedField]
    public int Stacks;

    [AutoNetworkedField]
    public TimeSpan Last;

    // Mobs don't block the sweeper until then, so the dash carries through its target.
    [AutoNetworkedField]
    public TimeSpan PassUntil;

    // Tempest: whether this sweep's follow-up spin has been used.
    [AutoNetworkedField]
    public bool TempestUsed;

    public readonly Dictionary<EntityUid, TimeSpan> Marked = new();
}

// Shots a Shot heavy has ready and when the next comes back.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMeleeShotStateComponent : Component
{
    [AutoNetworkedField]
    public int Charges = 1;

    [AutoNetworkedField]
    public int MaxCharges = 1;

    [AutoNetworkedField]
    public TimeSpan NextCharge;

    // When and where the last shot went, so the wielder is seen drawing the gun and firing.
    [AutoNetworkedField]
    public TimeSpan LastShot;

    [AutoNetworkedField]
    public Angle LastAim;
}

// Carries the user at a set speed with friction off, so a dash travels rather than jumps.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMeleeDashComponent : Component
{
    [AutoNetworkedField]
    public Vector2 Velocity;

    [AutoNetworkedField]
    public TimeSpan Until;
}

// Cuts the damage the user takes during a dash, or for as long as a weapon is winding up.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMeleeGuardComponent : Component
{
    [AutoNetworkedField]
    public TimeSpan Until;

    [AutoNetworkedField]
    public EntityUid? Charging;
}
