using System.Numerics;
using Content.Shared.Damage;
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

// Rev built up by hits in a row.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMeleeRevComponent : Component
{
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

    // When the last sweep's dash ends.
    [AutoNetworkedField]
    public TimeSpan Landed;

    [AutoNetworkedField]
    public TimeSpan NextSweep;

    // Not networked, so a re-predicted sweep ignores marks made on the tick it's replaying.
    public readonly Dictionary<EntityUid, (TimeSpan At, TimeSpan Until)> Marked = new();
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

    // Whether the holder carries the gun, so a heavy without one doesn't draw it.
    [AutoNetworkedField]
    public bool Armed = true;
}

// Carries the user at a set speed with friction off, so a dash travels rather than jumps.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMeleeDashComponent : Component
{
    [AutoNetworkedField]
    public Vector2 Velocity;

    [AutoNetworkedField]
    public TimeSpan Until;

    // A sweep's cut, dealt to each mob the dash reaches: its target in full, anything else in the way for less.
    public EntityUid? Weapon;
    public EntityUid? Primary;
    public DamageSpecifier? Damage;
    public FSMeleeJuiceProfile? Profile;
    public readonly HashSet<EntityUid> Hit = new();
}

// Cuts the damage the user takes during a dash, or for as long as a weapon is winding up.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMeleeGuardComponent : Component
{
    [AutoNetworkedField]
    public TimeSpan Until;

    // Server only: the weapon may be sold or deleted while this still points at it.
    public EntityUid? Charging;
}
