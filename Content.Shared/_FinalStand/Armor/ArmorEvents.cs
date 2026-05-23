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

// TODO(finalstand): raised as pre-damage hook by AP/Shred upgrade projectiles (pistol upgrades ticket)
public sealed class FinalStandDamageEvent : EntityEventArgs
{
    public FinalStandDamageFlags Flags;
    public EntityUid? Origin;
    public float ArmorAbsorbed;
}
