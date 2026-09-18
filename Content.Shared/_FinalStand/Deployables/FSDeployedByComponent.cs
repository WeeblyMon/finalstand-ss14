using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Deployables;

// Stamped on a placed structure: the sole record of who put it there.
[RegisterComponent]
public sealed partial class FSDeployedByComponent : Component
{
    public EntityUid? OwnerMind;

    public EntityUid? DeployedBy;

    public EntProtoId SourceProto;
}
