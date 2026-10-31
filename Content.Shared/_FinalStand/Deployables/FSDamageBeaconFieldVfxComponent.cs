using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Deployables;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true), AutoGenerateComponentPause]
public sealed partial class FSDamageBeaconFieldVfxComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan SpawnedAt;

    public bool IntroStarted;
    public bool Settled;
}
