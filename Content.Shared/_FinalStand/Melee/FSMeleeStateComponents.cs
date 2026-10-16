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
}
