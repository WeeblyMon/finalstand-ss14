using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Akimbo;

/// <summary>
///     Marks a gun as akimbo. One trigger pull fires two projectiles from the same magazine.
///     Networked so the client can display the AKIMBO HUD indicator.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FSAkimboGunComponent : Component
{
    /// <summary>
    /// Guards against recursive TakeAmmoEvent handling when we call RaiseLocalEvent for the second round.
    /// Not networked — runtime state only.
    /// </summary>
    public bool FiringSecondShot;
}
