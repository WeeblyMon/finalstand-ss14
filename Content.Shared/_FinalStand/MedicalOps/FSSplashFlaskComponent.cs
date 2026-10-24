using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSSplashFlaskComponent : Component
{
    [DataField]
    public string Solution = "flask";
}
