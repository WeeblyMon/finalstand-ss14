using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Weapons;

// marks a BallisticAmmoProviderComponent item that loads its whole contents into a weapon in one shot
[RegisterComponent, NetworkedComponent]
public sealed partial class FSBulkLoaderComponent : Component
{
}
