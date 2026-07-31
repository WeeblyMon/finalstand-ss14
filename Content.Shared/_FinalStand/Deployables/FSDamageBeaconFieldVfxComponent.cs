using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Deployables;

// SpawnedAt is a real server-stamped timestamp so client-side re-derivation of animation state (e.g. after a PVS rebuild) never restarts the intro from scratch.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSDamageBeaconFieldVfxComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan SpawnedAt;
}
