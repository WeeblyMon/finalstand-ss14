using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSRevenantGrabbedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Puller;

    [DataField, AutoNetworkedField]
    public float PullSpeed = 16f;

    [DataField, AutoNetworkedField]
    public float StopRange = 1f;

    [DataField, AutoNetworkedField]
    public TimeSpan EndsAt;
}
