using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Ailments;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class FSAilmentStateComponent : Component
{
    [AutoNetworkedField]
    public Dictionary<FSAilmentSlot, byte> Warnings = new();

    [AutoNetworkedField]
    public TimeSpan? DownEnd;

    [AutoNetworkedField]
    public bool Held;

    [AutoNetworkedField]
    public bool Regenerating;

    public readonly Dictionary<FSAilmentMeter, FSAilmentMeterState> Meters = new();

    public readonly Dictionary<FSAilmentSlot, string> Causes = new();

    public readonly Queue<(TimeSpan Time, float Damage)> DownSamples = new();

    public int PeakBleed;

    public TimeSpan LastDamage;
}
