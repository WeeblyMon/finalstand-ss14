using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared.Storage.Components;

/// <summary>
/// Applies an ongoing pickup area around the attached entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
[AutoGenerateComponentPause]
public sealed partial class MagnetPickupComponent : Component
{
    [DataField]
    [AutoPausedField]
    [AutoNetworkedField]
    public TimeSpan NextScan = TimeSpan.Zero;

    /// <summary>
    /// What container slot the magnet needs to be in to work.
    /// </summary>
    [DataField]
    public SlotFlags? SlotFlags = Inventory.SlotFlags.BELT;

    [DataField]
    public bool RequireActiveHand = false;

    [DataField]
    public float Range = 1f;

    [DataField, AutoNetworkedField]
    public bool MagnetEnabled = true;

    /// <summary>
    /// If false, MagnetEnabled is controlled by something else (e.g. an ItemToggle) instead of the
    /// verb added by this component.
    /// </summary>
    [DataField]
    public bool MagnetCanBeEnabled = true;

    [DataField]
    public int MagnetTogglePriority = 3;
}
