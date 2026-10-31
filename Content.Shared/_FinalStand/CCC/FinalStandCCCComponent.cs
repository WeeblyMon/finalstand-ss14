using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._FinalStand.CCC;

// The Central Command Console: the objective the crew has to keep alive.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FinalStandCCCComponent : Component
{
    // Read off the Destructible threshold on map init, so nothing hardcodes it.
    [AutoNetworkedField]
    public float MaxHealth;

    [DataField, AutoNetworkedField]
    public int Lives = 3;

    [AutoNetworkedField]
    public int LivesLeft = 3;

    // Brief invulnerability after losing a life.
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ShieldUntil;

    [DataField]
    public float HealFractionPerWave = 0.06f;
}

[Serializable, NetSerializable]
public sealed class FSCCCLifeLostEvent(NetEntity ccc, int livesLeft, int lives) : EntityEventArgs
{
    public readonly NetEntity Ccc = ccc;
    public readonly int LivesLeft = livesLeft;
    public readonly int Lives = lives;
}
