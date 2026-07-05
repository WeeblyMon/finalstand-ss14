using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Grenades;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSActionCounterComponent : Component
{
    [DataField, AutoNetworkedField] public int Current;
    [DataField, AutoNetworkedField] public int Max;
}
