using Content.Shared.Damage.Components;

namespace Content.Shared._FinalStand.Repair;

public static class FSStructureHealth
{
    public static float Fraction(FSStructureHealthComponent health, DamageableComponent damageable)
    {
        return health.Max <= 0f ? 1f : Math.Clamp(1f - damageable.TotalDamage.Float() / health.Max, 0f, 1f);
    }
}
