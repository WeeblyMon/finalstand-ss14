using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Upgrades.Effects;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(FSKnockedBackSystem))]
public sealed partial class FSKnockedBackComponent : Component
{
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan EndTime;
}
