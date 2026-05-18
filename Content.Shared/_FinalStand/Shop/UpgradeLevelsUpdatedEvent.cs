using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Shop;

[Serializable, NetSerializable]
public sealed class UpgradeLevelsUpdatedEvent : EntityEventArgs
{
    public readonly Dictionary<string, int> Levels;
    public readonly string WeaponTitle;

    public UpgradeLevelsUpdatedEvent(Dictionary<string, int> levels, string weaponTitle = "")
    {
        Levels = levels;
        WeaponTitle = weaponTitle;
    }
}
