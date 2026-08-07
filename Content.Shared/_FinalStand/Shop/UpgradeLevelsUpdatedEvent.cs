using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Shop;

[Serializable, NetSerializable]
public sealed class UpgradeLevelsUpdatedEvent : EntityEventArgs
{
    public readonly Dictionary<string, int> Levels;
    public readonly string WeaponTitle;

    // Accuracy is computed server-side from the weapon's real spread angles. AngleIncrease is
    // not a networked field, so the client cannot derive this itself.
    // -1 means the shop sells no gun, or the player owns no copy.
    public readonly int Accuracy;

    /// <summary>Accuracy the weapon would have at one level above its current, per upgrade id.</summary>
    public readonly Dictionary<string, int> NextLevelAccuracy;

    public UpgradeLevelsUpdatedEvent(Dictionary<string, int> levels, string weaponTitle = "",
        int accuracy = -1, Dictionary<string, int>? nextLevelAccuracy = null)
    {
        Levels = levels;
        WeaponTitle = weaponTitle;
        Accuracy = accuracy;
        NextLevelAccuracy = nextLevelAccuracy ?? new Dictionary<string, int>();
    }
}
