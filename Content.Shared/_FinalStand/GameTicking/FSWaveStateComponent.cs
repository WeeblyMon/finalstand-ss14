using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._FinalStand.GameTicking;

// What every client shows about the current wave. Lives on the wave rule entity, which is sent to all
// clients; the server copies its values in from the rule each tick and only dirties what changed.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
public sealed partial class FSWaveStateComponent : Component
{
    // Waves completed during prep, the wave being fought during combat.
    [DataField, AutoNetworkedField]
    public int Wave;

    [DataField, AutoNetworkedField]
    public WavePhase Phase;

    // End of the current phase: prep, the start countdown, a dark wave, or combat's fallback limit.
    // Clients count down to it themselves.
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan PhaseEndsAt;

    [DataField, AutoNetworkedField]
    public int EnemiesAlive;

    [DataField, AutoNetworkedField]
    public int EnemiesTotal;

    [DataField, AutoNetworkedField]
    public bool DarkWave;
}
