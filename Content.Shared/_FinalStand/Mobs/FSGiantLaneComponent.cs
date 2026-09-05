using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mobs;

// telegraph lane: one stretched rectangle rather than a row of per-tile markers
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSGiantLaneComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Length = 1f;

    [DataField, AutoNetworkedField]
    public float Width = 1f;
}
