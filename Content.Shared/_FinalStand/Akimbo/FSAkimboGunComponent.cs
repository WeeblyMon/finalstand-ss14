using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Akimbo;

/// <summary>
///     Placed on each gun in an akimbo pair. Tracks the partner gun and which hands both are held in.
///     Networked so the client can show the AKIMBO HUD indicator.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FSAkimboGunComponent : Component
{
    [DataField] public EntityUid? PairedGun;

    /// <summary>Hand name (from HandsComponent.Hands) this gun is held in.</summary>
    [DataField] public string? MyHand;

    /// <summary>Hand name the paired gun is held in.</summary>
    [DataField] public string? PairedHand;
}
