using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Deployables;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSPackableComponent : Component
{
    [DataField(required: true)]
    public EntProtoId PackedProtoId;

    [DataField]
    public TimeSpan PackTime = TimeSpan.FromSeconds(8);

    [DataField]
    public bool MedicalOnly = true;
}
