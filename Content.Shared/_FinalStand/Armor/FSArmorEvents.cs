namespace Content.Shared._FinalStand.Armor;

[ByRefEvent]
public readonly record struct ArmorDepletedEvent;

[ByRefEvent]
public readonly record struct FSEnemyHpScaledEvent;

[ByRefEvent]
public readonly record struct FSArmorAbsorbedEvent(EntityUid? Shooter, float Absorbed);

[Flags]
public enum FinalStandDamageFlags
{
    None = 0,
    ArmorPenetrating = 1 << 0,
    ArmorShred = 1 << 1,
}
