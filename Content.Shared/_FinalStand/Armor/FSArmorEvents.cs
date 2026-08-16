namespace Content.Shared._FinalStand.Armor;

public sealed class ArmorDepletedEvent : EntityEventArgs { }

public sealed class FSEnemyHpScaledEvent : EntityEventArgs { }

public sealed class FSArmorAbsorbedEvent : EntityEventArgs
{
    public EntityUid? Shooter;
    public float Absorbed;
}

[Flags]
public enum FinalStandDamageFlags
{
    None = 0,
    ArmorPenetrating = 1 << 0,
    ArmorShred = 1 << 1,
}
