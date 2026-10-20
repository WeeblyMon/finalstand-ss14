using Content.Server._FinalStand.NPC;
using Content.Server._FinalStand.Perks;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Crit;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Projectiles;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Crit;

public sealed partial class CritSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private FSZombieRetaliationSystem _retaliation = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FSPerkCritSystem _perkCrit = default!;
    private readonly HashSet<(EntityUid, EntityUid)> _pendingCrits = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<WaveSpawnedTagComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    // Armour absorbing a crit means DamageChangedEvent never fires, so unclaimed marks expire here.
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _pendingCrits.Clear();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent _) => _pendingCrits.Clear();

    public float CalculateCritChance(EntityUid gun)
    {
        var weaponCrit = TryComp<CritComponent>(gun, out var critComp) ? critComp.BaseCritChance : 0f;
        var upgradeCrit = TryComp<FSWeaponUpgradeStateComponent>(gun, out var upgradeState) ? upgradeState.CritChance : 0f;
        return MathF.Min(1f - (1f - weaponCrit) * (1f - upgradeCrit), 1f);
    }

    public float CalculateCritMultiplier(EntityUid gun)
    {
        return FSCritMath.Multiplier(CompOrNull<CritComponent>(gun), CompOrNull<FSWeaponUpgradeStateComponent>(gun));
    }

    public bool TryRollCrit(EntityUid shooter, EntityUid gun, EntityUid target, out float multiplier)
    {
        multiplier = 1f;
        if (HasComp<CritImmuneComponent>(target))
            return false;
        var chance = FSPerkCritSystem.CombineChance(CalculateCritChance(gun), _perkCrit.GetCritChanceBonus(shooter, gun, melee: false));
        if (_random.NextFloat() >= chance)
            return false;
        multiplier = CalculateCritMultiplier(gun) + _perkCrit.GetCritMultiplierBonus(shooter, gun);
        return true;
    }

    public void MarkPendingCrit(EntityUid shooter, EntityUid target)
    {
        _pendingCrits.Add((shooter, target));
    }

    private void OnProjectileHit(EntityUid uid, ProjectileComponent comp, ref ProjectileHitEvent args)
    {
        if (comp.Shooter is not { } shooterUid || comp.Weapon is not { } weapon)
            return;

        // A bullet still in flight after its shooter was deleted can't crit and skips the shooter's effects, but
        // target and weapon effects (armoured deflect, research damage) still apply. With nobody left to ask,
        // friendly fire goes by the target alone.
        EntityUid? shooter = TerminatingOrDeleted(shooterUid) ? null : shooterUid;
        if ((shooter == null || HasComp<FSFriendlyFireComponent>(shooterUid)) && HasComp<FSFriendlyFireComponent>(args.Target))
            return;

        var didCrit = false;
        if (shooter is { } s && TryRollCrit(s, weapon, args.Target, out var multiplier))
        {
            didCrit = true;
            args.Damage *= multiplier;
            _pendingCrits.Add((s, args.Target));
        }

        var hitEffect = new FSProjectileHitEffectEvent(args.Target, args.Damage)
        {
            Weapon        = weapon,
            Shooter       = shooter,
            ProjectileUid = uid,
            WasCrit       = didCrit,
            State         = CompOrNull<FSWeaponUpgradeStateComponent>(weapon),
        };
        RaiseLocalEvent(ref hitEffect);
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
        var landed = new CritLandedEvent(uid, args.Origin.Value, amount, wasCrit);
        RaiseLocalEvent(uid, ref landed);
    }
}
