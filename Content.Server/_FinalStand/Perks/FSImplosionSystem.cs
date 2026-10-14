using Content.Server._FinalStand.Grenades;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Explosion.Components;
using Content.Shared.Mind;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;

namespace Content.Server._FinalStand.Perks;

// Implosion: every tile's intensity scales by the damage bonus while the radius shrinks.
// Slope and total intensity scale with it so the explosion keeps its shape, only smaller.
public sealed partial class FSImplosionSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;

    public readonly record struct Factors(float Max, float Slope, float Total);

    public bool TryGetFactors(EntityUid? owner, out Factors factors)
    {
        factors = default;
        if (owner is not { } body
            || !_mind.TryGetMind(body, out var mindId, out _)
            || !TryComp<FSPerkLevelsComponent>(mindId, out var perks))
            return false;

        var level = perks.GetSlottedLevel("Implosion");
        if (level <= 0)
            return false;

        var damage = 1f + FSPerkBonusConstants.ImplosionDamage[Math.Min(level, FSPerkDef.MaxLevel) - 1];
        var radius = FSPerkBonusConstants.ImplosionRadiusMultiplier;
        factors = new Factors(damage, damage / radius, damage * radius * radius);
        return true;
    }

    /// <summary>Applies Implosion to a grenade or rocket about to explode, once.</summary>
    public void ApplyToExplosive(EntityUid uid, EntityUid? triggerUser)
    {
        if (HasComp<FSImplosionAppliedComponent>(uid) || !TryComp<ExplosiveComponent>(uid, out var explosive))
            return;

        if (!TryGetFactors(OwnerOf(uid) ?? triggerUser, out var factors))
            return;

        EnsureComp<FSImplosionAppliedComponent>(uid);
#pragma warning disable RA0002
        explosive.MaxIntensity *= factors.Max;
        explosive.IntensitySlope *= factors.Slope;
        explosive.TotalIntensity *= factors.Total;
#pragma warning restore RA0002
    }

    private EntityUid? OwnerOf(EntityUid uid)
    {
        if (TryComp<FSGrenadeOwnerComponent>(uid, out var grenade) && grenade.Thrower.IsValid())
            return grenade.Thrower;
        if (TryComp<ProjectileComponent>(uid, out var projectile) && projectile.Shooter != null)
            return projectile.Shooter;
        if (TryComp<ThrownItemComponent>(uid, out var thrown) && thrown.Thrower != null)
            return thrown.Thrower;
        return null;
    }
}

[RegisterComponent]
public sealed partial class FSImplosionAppliedComponent : Component;
