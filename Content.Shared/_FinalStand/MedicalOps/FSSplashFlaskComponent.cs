using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSSplashFlaskComponent : Component
{
    [DataField]
    public string Solution = "flask";

    [DataField]
    public EntProtoId CloudProto = "FSSplashCloud";

    [DataField]
    public float Duration = 10f;

    [DataField]
    public int SpreadAmount = 6;
}
