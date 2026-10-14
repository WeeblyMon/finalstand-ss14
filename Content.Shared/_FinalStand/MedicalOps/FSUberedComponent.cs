using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSUberedComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;

    [DataField, AutoNetworkedField]
    public float Duration = 10f;

    [DataField, AutoNetworkedField]
    public Color SourceColor = Color.FromHex("#FF6A3D");

    [DataField, AutoNetworkedField]
    public bool InfiniteStamina;

    [ViewVariables]
    public TimeSpan NextTing;
}

[Serializable, NetSerializable]
public sealed class FSUberActivateMessage : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class FSUberDeployedEvent(NetEntity medic, Color color) : EntityEventArgs
{
    public readonly NetEntity Medic = medic;
    public readonly Color Color = color;
}

[Serializable, NetSerializable]
public sealed class FSUberBlockedEvent(NetEntity target) : EntityEventArgs
{
    public readonly NetEntity Target = target;
}
