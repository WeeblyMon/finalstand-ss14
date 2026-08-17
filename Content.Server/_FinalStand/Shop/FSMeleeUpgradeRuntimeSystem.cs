using Content.Server._FinalStand.Crit;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Upgrades;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Damage.Systems;
using Content.Server.Popups;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Shop;

// melee upgrade effects; all on directed (FSWeaponUpgradeStateComponent, MeleeHitEvent) — undirected MeleeHitEvent is dead
public sealed partial class FSMeleeUpgradeRuntimeSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private StaminaSystem _stamina = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private CritSystem _crit = default!;
    [Dependency] private FSStunOverrideSystem _fsStun = default!;
    [Dependency] private PopupSystem _popup = default!;

    private EntityQuery<FSFriendlyFireComponent> _ffQuery;

    private const float StaminaDrainPerLevel = 15f;
    private const float StaminaRestorePerLevel = 10f;
    private const float SetOnFireStacksPerHit = 3f;
    private const float BleedScalePerLevel = 0.2f;
    private const float WhileBurningDamageMultiplier = 2.0f;
    private static readonly TimeSpan BleedDuration = TimeSpan.FromSeconds(3);

    public override void Initialize()
    {
        base.Initialize();
        _ffQuery = GetEntityQuery<FSFriendlyFireComponent>();
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, GetMeleeAttackRateEvent>(OnGetMeleeAttackRate);
    }

    private void OnMeleeHit(EntityUid weapon, FSWeaponUpgradeStateComponent state, MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        var user = args.User;
        var totalBaseDamage = args.BaseDamage.GetTotal().Float();
        var heavyAttack = args.Direction != null;

        if (state.WhileBurningBuff
            && TryComp<FlammableComponent>(user, out var wielderFlammable)
            && wielderFlammable.OnFire)
        {
            args.BonusDamage += args.BaseDamage * (WhileBurningDamageMultiplier - 1f);
        }

        var didCrit = false;
        if (state.CritChance > 0f && _random.NextFloat() < state.CritChance)
        {
            didCrit = true;
            args.BonusDamage += args.BaseDamage * (state.CritDamageMultiplier - 1f);
        }

        foreach (var target in args.HitEntities)
        {
            // stun before CritVsStunned so the same swing can trigger both
            if (state.ConcussionClubStunMs > 0 && heavyAttack)
            {
                if (_fsStun.TryForceStun(target, TimeSpan.FromMilliseconds(state.ConcussionClubStunMs)))
                    _popup.PopupEntity("Stunned!", target, PopupType.SmallCaution);
            }
            if (state.StunOnHitMs > 0)
            {
                if (_fsStun.TryForceStun(target, TimeSpan.FromMilliseconds(state.StunOnHitMs)))
                    _popup.PopupEntity("Stunned!", target, PopupType.SmallCaution);
            }

            if (state.CritVsStunned && HasComp<StunnedComponent>(target))
            {
                _damageable.TryChangeDamage(target, args.BaseDamage, origin: user);
                _crit.MarkPendingCrit(user, target);
            }

            if (state.CritVsBurning
                && TryComp<FlammableComponent>(target, out var targetFlammable)
                && targetFlammable.OnFire)
            {
                _damageable.TryChangeDamage(target, args.BaseDamage, origin: user);
                _crit.MarkPendingCrit(user, target);
            }

            if (state.SetOnFireEnabled && !_ffQuery.HasComponent(target) && TryComp<FlammableComponent>(target, out var igniteFlammable))
            {
                igniteFlammable.FireStacks += SetOnFireStacksPerHit;
                _flammable.Ignite(target, user, igniteFlammable, ignitionSourceUser: user);
            }

            if (state.StaminaStealLevel > 0)
            {
                if (HasComp<StaminaComponent>(target))
                    _stamina.TakeStaminaDamage(target, state.StaminaStealLevel * StaminaDrainPerLevel, source: user);
                if (HasComp<StaminaComponent>(user))
                    _stamina.TakeStaminaDamage(user, -(state.StaminaStealLevel * StaminaRestorePerLevel));
            }

            if (state.BleedLevel > 0 && totalBaseDamage > 0f && !_ffQuery.HasComponent(target))
            {
                var dps = totalBaseDamage * BleedScalePerLevel * state.BleedLevel;
                if (dps > 0f)
                {
                    var bleed = EnsureComp<FSBleedingComponent>(target);
                    bleed.DamagePerSecond = dps;
                    bleed.ExpiresAt = _timing.CurTime + BleedDuration;
                    bleed.NextTickAt = _timing.CurTime;
                    bleed.Instigator = user;
                }
            }

            if (state.MoneyGainBonusPerKill > 0)
            {
                var bonus = EnsureComp<FSPendingKillBonusComponent>(target);
                bonus.MoneyBonus = state.MoneyGainBonusPerKill;
            }

            if (state.MoneyPerHitBonus > 0 && HasComp<WaveSpawnedTagComponent>(target) && _mind.TryGetMind(user, out var mindId, out _))
                _wallet.GiveCredits(mindId, state.MoneyPerHitBonus);
        }

        if (state.LifeStealPercent > 0f && totalBaseDamage > 0f)
        {
            var healAmount = totalBaseDamage * state.LifeStealPercent * args.HitEntities.Count;
            _damageable.HealEvenly(user, FixedPoint2.New(-healAmount));
        }

        if (didCrit)
        {
            foreach (var target in args.HitEntities)
                _crit.MarkPendingCrit(user, target);
        }
    }

    private void OnGetMeleeDamage(EntityUid weapon, FSWeaponUpgradeStateComponent state, ref GetMeleeDamageEvent args)
    {
        if (state.DamageMultiplier > 1f)
            args.Damage *= state.DamageMultiplier;

        if (state.PierceThreshold > FixedPoint2.Zero)
            args.ResistanceBypass = true;
    }

    private void OnGetMeleeAttackRate(EntityUid weapon, FSWeaponUpgradeStateComponent state, ref GetMeleeAttackRateEvent args)
    {
        if (state.AttackSpeedMultiplier > 1f)
            args.Multipliers *= state.AttackSpeedMultiplier;
    }
}
