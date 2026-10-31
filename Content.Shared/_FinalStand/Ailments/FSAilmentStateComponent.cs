using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Ailments;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class FSAilmentStateComponent : Component
{
    [AutoNetworkedField]
    public Dictionary<FSAilmentSlot, byte> Warnings = new();

    [AutoNetworkedField]
    public Dictionary<FSAilmentSlot, byte> Mend = new();

    [AutoNetworkedField]
    public TimeSpan? DownEnd;

    [AutoNetworkedField]
    public bool Held;

    [AutoNetworkedField]
    public bool Regenerating;

    [AutoNetworkedField]
    public bool Recovering;

    // DownEnd, LastDamage, RecoveringSince, the meters and the down samples are all times; FSAilmentSystem moves
    // them on when the body is unpaused (the pause generator can't reach inside the collections).
    public readonly Dictionary<FSAilmentMeter, FSAilmentMeterState> Meters = new();

    public readonly Dictionary<FSAilmentSlot, float> MendProgress = new();

    public readonly Queue<(TimeSpan Time, float Damage)> DownSamples = new();

    public int PeakBleed;

    public float LastBleedAmount;

    public TimeSpan LastDamage;

    public EntityUid? FireSource;

    public TimeSpan? RecoveringSince;
}
