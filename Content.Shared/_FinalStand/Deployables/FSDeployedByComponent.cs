using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Deployables;

// Stamped on a placed structure: the sole record of who put it there.
// OwnerMind is the durable identity and survives respawns; DeployedBy is the body, for damage attribution.
[RegisterComponent]
public sealed partial class FSDeployedByComponent : Component
{
    public EntityUid? OwnerMind;

    public EntityUid? DeployedBy;

    public EntProtoId SourceProto;
}
