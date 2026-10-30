using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Humanoid;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FSBodySizeComponent : Component
{
    [DataField, AutoNetworkedField]
    public Vector2 Scale = Vector2.One;
}

[ByRefEvent]
public readonly record struct FSBodySizeChangedEvent;
