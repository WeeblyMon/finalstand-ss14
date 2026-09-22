using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Deployables;

// Stamped on a placed structure: the sole record of who put it there.
[RegisterComponent]
public sealed partial class FSDeployedByComponent : Component
{
    public EntityUid? OwnerMind;

    public EntityUid? DeployedBy;

    public EntProtoId SourceProto;

    // The item entity that was consumed to deploy this. Packing restores stock onto this specific
    // entity instead of spawning a fresh one, so pack/deploy can't be looped to duplicate stock.
    public EntityUid? SourceItem;
}
