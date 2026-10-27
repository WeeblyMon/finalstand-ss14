using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Ailments;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSSignatureAilmentComponent : Component
{
    [DataField(required: true)]
    public Dictionary<FSAilmentSlot, int> Ailments = new();

    [DataField(required: true)]
    public LocId Examine;
}
