using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Ailments;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSAilmentCureComponent : Component
{
    [DataField(required: true)]
    public FSAilmentSlot Slot;

    [DataField]
    public EntProtoId? Ward;

    [DataField]
    public float Mend;

    [ViewVariables]
    public float? BaseMend;
}
