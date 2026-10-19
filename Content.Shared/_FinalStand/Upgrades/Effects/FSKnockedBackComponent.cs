using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Upgrades.Effects;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(FSKnockedBackSystem))]
public sealed partial class FSKnockedBackComponent : Component
{
    [AutoNetworkedField]
    public TimeSpan EndTime;
}
