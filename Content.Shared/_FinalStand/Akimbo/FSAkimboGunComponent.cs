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
    /// Extra spread added to min/max angle while in akimbo mode.
    /// </summary>
    [DataField]
    public float SpreadPenalty = 10f;

    /// <summary>
    /// Perpendicular offset in tiles applied to the second projectile's origin,
    /// simulating a second muzzle position.
    /// </summary>
    [DataField]
    public float MuzzleOffset = 0.3f;

    /// <summary>
    /// Guards against recursive GunShotEvent handling when we call Shoot() for the second projectile.
    /// Not networked — runtime state only.
    /// </summary>
    public bool FiringSecondShot;
}
