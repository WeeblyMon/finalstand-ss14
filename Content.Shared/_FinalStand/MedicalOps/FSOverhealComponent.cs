using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSOverhealComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Amount;

    [DataField, AutoNetworkedField]
    public float Max;

    [DataField, AutoNetworkedField]
    public Color SourceColor = Color.FromHex("#3FD7E0");

    [DataField]
    public float DecayPerSecond;

    [DataField]
    public TimeSpan LastFed;
}

[Serializable, NetSerializable]
public sealed class FSOverhealBrokenEvent(NetEntity patient) : EntityEventArgs
{
    public readonly NetEntity Patient = patient;
}
