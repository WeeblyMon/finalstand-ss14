using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Ailments;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class FSAilmentStateComponent : Component
{
    [AutoNetworkedField]
    public Dictionary<FSAilmentSlot, byte> Warnings = new();

    public readonly Dictionary<FSAilmentMeter, FSAilmentMeterState> Meters = new();

    public readonly Dictionary<FSAilmentSlot, string> Causes = new();
}
