using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Deployables;

// Stamped on a placed structure so FSDeployableSystem can count a player's standing deployables against MaxDeployed.
[RegisterComponent]
public sealed partial class FSDeployedByComponent : Component
{
    public EntityUid? OwnerMind;

    public EntProtoId SourceProto;
}
