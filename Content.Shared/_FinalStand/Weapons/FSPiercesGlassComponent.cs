using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Weapons;

// projectile passes through glass — anything that stops bullets without blocking sight
[RegisterComponent, NetworkedComponent]
public sealed partial class FSPiercesGlassComponent : Component
{
}
