namespace Content.Shared._FinalStand.Shop;

/// <summary>
/// Tracks how much magazine-size bonus has already been applied to this magazine entity
/// so re-inserting the same mag doesn't stack the bonus again.
/// </summary>
[RegisterComponent]
public sealed partial class FSMagUpgradedComponent : Component
{
    public int AppliedBonus = 0;
}
