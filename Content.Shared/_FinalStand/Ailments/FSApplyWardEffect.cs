using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Ailments;

public sealed partial class FSApplyWard : EntityEffectBase<FSApplyWard>
{
    [DataField(required: true)]
    public EntProtoId Ward;

    [DataField]
    public ProtoId<FSTechNodePrototype>? RequiresNode;
}
