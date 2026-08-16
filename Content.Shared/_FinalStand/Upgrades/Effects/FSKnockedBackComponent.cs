using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Upgrades.Effects;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSKnockedBackComponent : Component
{
    [AutoNetworkedField]
    public TimeSpan EndTime;
}
