using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Deployables;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSBankedLifetimeComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Remaining;
}
