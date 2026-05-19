using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Crit;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Damage.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Crit;

public sealed class CritSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    // (shooter, target) pairs where a crit was rolled this frame, consumed on DamageChangedEvent.
    private readonly HashSet<(EntityUid, EntityUid)> _pendingCrits = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<WaveSpawnedTagComponent, DamageChangedEvent>(OnDamageChanged);
    }

    // ---- Public API ----

    /// <summary>
    ///     Aggregates crit chance from all sources multiplicatively and clamps to [0, 1].
    ///     Sources: CritComponent on the gun, PerkCritChanceContribution on the shooter.
    ///     formula: 1 - ((1 - A) * (1 - B) * ...)
    /// </summary>
    public float CalculateCritChance(EntityUid shooter, EntityUid gun)
    {
        var weaponCrit = TryComp<CritComponent>(gun, out var critComp) ? critComp.BaseCritChance : 0f;
        var perkCrit = TryComp<PerkComponent>(shooter, out var perks) ? perks.PerkCritChanceContribution : 0f;
        // TODO(finalstand): add gear crit chance when gear system exists

        var total = 1f - (1f - weaponCrit) * (1f - perkCrit);
        return MathF.Min(total, 1f);
    }

    /// <summary>Returns the combined crit damage multiplier for this shooter + gun pair.</summary>
    public float CalculateCritMultiplier(EntityUid shooter, EntityUid gun)
    {
        // TODO(finalstand): add multiplier contributions from perks/gear here
        return TryComp<CritComponent>(gun, out var critComp) ? critComp.CritMultiplier : 1.5f;
    }

    /// <summary>
    ///     Rolls a crit. Returns false if the target is crit-immune or the roll fails.
    ///     Sets <paramref name="multiplier"/> to the crit multiplier on success, 1.0 on failure.
    /// </summary>
    public bool TryRollCrit(EntityUid shooter, EntityUid gun, EntityUid target, out float multiplier)
    {
        multiplier = 1f;
        if (HasComp<CritImmuneComponent>(target))
            return false;
        var chance = CalculateCritChance(shooter, gun);
        if (_random.NextFloat() >= chance)
            return false;
        multiplier = CalculateCritMultiplier(shooter, gun);
        return true;
    }

    // ---- Event handlers ----

    private void OnProjectileHit(EntityUid uid, ProjectileComponent comp, ref ProjectileHitEvent args)
    {
        if (comp.Shooter == null || comp.Weapon == null)
            return;

        if (!TryRollCrit(comp.Shooter.Value, comp.Weapon.Value, args.Target, out var multiplier))
            return;

        // Multiply the outgoing DamageSpecifier before TryChangeDamage runs.
        // Note: this applies before DamageModifyEvent (armor relay). If a meaningful per-entity
        // armor system is added for wave enemies, move this into a DamageModifyEvent handler on
        // the target so the crit multiplier applies after armor reduction.
        args.Damage = args.Damage * multiplier;
        _pendingCrits.Add((comp.Shooter.Value, args.Target));
    }

    private void OnDamageChanged(EntityUid uid, WaveSpawnedTagComponent _, ref DamageChangedEvent args)
    {
        if (args.DamageDelta == null || !args.DamageIncreased || args.Origin == null)
            return;

        var amount = args.DamageDelta.GetTotal().Float();
        if (amount <= 0f)
            return;

        // _pendingCrits only stores true-crit pairs; Remove returns false (no entry) for normal hits.
        var isCrit = _pendingCrits.Remove((args.Origin.Value, uid));

        RaiseLocalEvent(uid, new CritLandedEvent
        {
            Target      = uid,
            Shooter     = args.Origin.Value,
            FinalDamage = amount,
            WasCrit     = isCrit,
        });
    }
}
