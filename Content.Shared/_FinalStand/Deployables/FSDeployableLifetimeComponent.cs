using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Deployables;

// Placed Null Field / Damage Beacon self-destruct after Lifetime instead of relying on zombies to destroy them.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSDeployableLifetimeComponent : Component
{
    [DataField]
    public TimeSpan Lifetime = TimeSpan.FromSeconds(45);

    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;
}
