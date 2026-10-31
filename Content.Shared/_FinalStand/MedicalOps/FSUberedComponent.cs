using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(FSUberSharedSystem))]
public sealed partial class FSUberedComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan EndTime;

    [DataField, AutoNetworkedField]
    public float Duration = 10f;

    [DataField, AutoNetworkedField]
    public Color SourceColor = Color.FromHex("#FF6A3D");

    [DataField, AutoNetworkedField]
    public bool InfiniteStamina;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextTing;

    // The gun whose beam gave this cover; it ends when that gun lets go of the patient.
    [ViewVariables]
    public EntityUid? Source;
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
