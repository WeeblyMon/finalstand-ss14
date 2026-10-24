using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSSurgeryAutoComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Patient;

    [DataField, AutoNetworkedField]
    public EntityUid Part;

    [ViewVariables]
    public TimeSpan NextAttempt;
}

[Serializable, NetSerializable]
public sealed class FSSurgeryAutoMessage(NetEntity part, bool enabled) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
    public readonly bool Enabled = enabled;
}
