using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.CCC;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FinalStandCCCTagComponent : Component
{
    // Published from the Destructible threshold on map init, so nothing hardcodes it.
    [AutoNetworkedField]
    public float MaxHealth;

    [DataField, AutoNetworkedField]
    public int Lives = 3;

    [AutoNetworkedField]
    public int LivesLeft = 3;

    [AutoNetworkedField]
    public TimeSpan ShieldUntil;
}

[Serializable, NetSerializable]
public sealed class FSCCCLifeLostEvent(NetEntity ccc, int livesLeft, int lives) : EntityEventArgs
{
    public readonly NetEntity Ccc = ccc;
    public readonly int LivesLeft = livesLeft;
    public readonly int Lives = lives;
}
