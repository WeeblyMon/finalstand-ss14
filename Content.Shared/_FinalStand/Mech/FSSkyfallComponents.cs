// Skyfall swarm pod: a burst of micro-missiles that climb out of reach, then dive onto zombies the mech can see.
using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mech;

[RegisterComponent]
public sealed partial class FSSkyfallLauncherComponent : Component
{
    [DataField]
    public int MaxLive = 12;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSSkyfallMissileComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Launched;

    [DataField]
    public float Ascend = 0.35f;

    [DataField]
    public float Dive = 0.2f;

    [DataField]
    public float Height = 0.8f;

    [DataField]
    public float TurnRate = 420f;

    [DataField]
    public float SearchRange = 9f;

    [DataField]
    public float SplashRadius = 1f;

    [DataField]
    public DamageSpecifier SplashDamage = new();

    [AutoNetworkedField]
    public EntityUid? LockedOn;

    public EntityUid? Launcher;
    public EntityUid? Shooter;
    public bool Diving;
}
