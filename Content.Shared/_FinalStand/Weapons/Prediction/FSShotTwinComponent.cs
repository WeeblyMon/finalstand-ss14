using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Weapons.Prediction;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSShotTwinComponent : Component
{
    [AutoNetworkedField]
    public EntityUid Shooter;

    [AutoNetworkedField]
    public GameTick Tick;

    [AutoNetworkedField]
    public int Index;

    [AutoNetworkedField]
    public bool Hideable;

    [ViewVariables]
    public bool ContactShown;
}
