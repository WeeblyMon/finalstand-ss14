using Content.Server._FinalStand.NPC;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Crit;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Crit;

public sealed partial class CritSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private FSZombieRetaliationSystem _retaliation = default!;
    [Dependency] private IGameTiming _timing = default!;
    private readonly HashSet<(EntityUid, EntityUid)> _pendingCrits = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<WaveSpawnedTagComponent, DamageChangedEvent>(OnDamageChanged);
    }
    public float CalculateCritChance(EntityUid shooter, EntityUid gun)
    {
        var weaponCrit = TryComp<CritComponent>(gun, out var critComp) ? critComp.BaseCritChance : 0f;
        var upgradeCrit = TryComp<FSWeaponUpgradeStateComponent>(gun, out var upgradeState) ? upgradeState.CritChance : 0f;
        return MathF.Min(1f - (1f - weaponCrit) * (1f - upgradeCrit), 1f);
    }

    public float CalculateCritMultiplier(EntityUid shooter, EntityUid gun)
    {
        if (TryComp<FSWeaponUpgradeStateComponent>(gun, out var upgradeState))
            return upgradeState.CritDamageMultiplier;
        return TryComp<CritComponent>(gun, out var critComp) ? critComp.CritMultiplier : 1.5f;
    }

    public bool TryRollCrit(EntityUid shooter, EntityUid gun, EntityUid target, out float multiplier)
    {
        multiplier = 1f;
        if (HasComp<CritImmuneComponent>(target))
            return false;
        if (_random.NextFloat() >= CalculateCritChance(shooter, gun))
            return false;
        multiplier = CalculateCritMultiplier(shooter, gun);
        return true;
    }

    public void MarkPendingCrit(EntityUid shooter, EntityUid target)
    {
        _pendingCrits.Add((shooter, target));
    }

    private void OnProjectileHit(EntityUid uid, ProjectileComponent comp, ref ProjectileHitEvent args)
    {
        if (comp.Shooter == null || comp.Weapon == null)
            return;

        var didCrit = TryRollCrit(comp.Shooter.Value, comp.Weapon.Value, args.Target, out var multiplier);
        if (didCrit)
        {
            args.Damage *= multiplier;
            _pendingCrits.Add((comp.Shooter.Value, args.Target));
        }

        var hitEffect = new FSProjectileHitEffectEvent
        {
            Target        = args.Target,
            Weapon        = comp.Weapon,
            Shooter       = comp.Shooter,
            ProjectileUid = uid,
            Damage        = args.Damage,
            WasCrit       = didCrit,
            State         = CompOrNull<FSWeaponUpgradeStateComponent>(comp.Weapon.Value),
        };
        RaiseLocalEvent(hitEffect);
        if (hitEffect.AdditionalMultiplier != 1f)
            args.Damage *= hitEffect.AdditionalMultiplier;
    }

    private void OnDamageChanged(EntityUid uid, WaveSpawnedTagComponent _, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased && args.Origin != null)
        {
            _retaliation.TryRetaliate(uid, args.Origin.Value);
        }

        if (args.DamageDelta == null || !args.DamageIncreased || args.Origin == null)
            return;
        var amount = args.DamageDelta.GetTotal().Float();
        if (amount <= 0f)
            return;

        var wasCrit = _pendingCrits.Remove((args.Origin.Value, uid));
        RaiseLocalEvent(uid, new CritLandedEvent
        {
            Target = uid,
            Shooter = args.Origin.Value,
            FinalDamage = amount,
            WasCrit = wasCrit,
        });
    }
}
