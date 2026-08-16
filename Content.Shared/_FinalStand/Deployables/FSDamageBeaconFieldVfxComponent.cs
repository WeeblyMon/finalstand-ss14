using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Deployables;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSDamageBeaconFieldVfxComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan SpawnedAt;

    public bool IntroStarted;
    public bool Settled;
}
