using Content.Server._FinalStand.Deployables;
using Content.Server._FinalStand.Perks;
using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.HMG;
using Content.Shared._FinalStand.Weapons;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;

namespace Content.Server._FinalStand.Economy;

public sealed partial class FSMoneyOnHitSystem : EntitySystem
{
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private FSUndyingSystem _undying = default!;

    private EntityQuery<FSWeaponUpgradeStateComponent> _upgradeQuery;
    private EntityQuery<FSArmorComponent> _armorQuery;
    private EntityQuery<FSHarvesterComponent> _harvesterQuery;

    private const int BaseMoneyPerHit = 30;

    private EntityUid? _deployableHitTarget;
    private (EntityUid Target, int Amount)? _flatHit;

    // The Harvester lands 20 hitscans a second, so at full rate it caps every enemy almost
    // instantly. Its payout is scaled down to keep it a research weapon, not an income weapon.
    private const float HarvesterMoneyMultiplier = 0.30f;
    public override void Initialize()
    {
        base.Initialize();
        _upgradeQuery = GetEntityQuery<FSWeaponUpgradeStateComponent>();
        _armorQuery = GetEntityQuery<FSArmorComponent>();
        _harvesterQuery = GetEntityQuery<FSHarvesterComponent>();

        SubscribeLocalEvent<FSMoneyOnHitCapComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<FSDeployableProjectileComponent, ProjectileHitEvent>(OnDeployableHit);
        SubscribeLocalEvent<FSFlatMoneyProjectileComponent, ProjectileHitEvent>(OnFlatHit);
    }

    private void OnDeployableHit(Entity<FSDeployableProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        _deployableHitTarget = args.Target;
    }

    private void OnFlatHit(Entity<FSFlatMoneyProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        _flatHit = (args.Target, ent.Comp.Amount);
    }

    private void OnDamageChanged(EntityUid uid, FSMoneyOnHitCapComponent cap, DamageChangedEvent args)
    {
        var fromDeployable = _deployableHitTarget == uid;
        _deployableHitTarget = null;
        int? flatAmount = _flatHit is { } flatHit && flatHit.Target == uid ? flatHit.Amount : null;
        _flatHit = null;

        if (!args.DamageIncreased || args.Origin == null)
            return;
        if (_mobState.IsDead(uid))
            return;
        if (_armorQuery.TryGetComponent(uid, out var armor) && armor.CurrentArmor > 0)
            return;

        if (_undying.IsActive(args.Origin.Value))
            return;
        if (!_mind.TryGetMind(args.Origin.Value, out var mindId, out _))
            return;

        cap.MoneyGivenPerPlayer.TryGetValue(mindId, out var alreadyGiven);
        if (alreadyGiven >= cap.MaxMoneyPerPlayer)
            return;

        var payout = flatAmount ?? BaseMoneyPerHit;
        if (flatAmount == null && !fromDeployable && _hands.TryGetActiveItem(args.Origin.Value, out var heldItem))
        {
            if (TryComp<FSFlatMoneyPerHitComponent>(heldItem, out var flat))
                payout = flat.Amount;
            else if (_upgradeQuery.TryGetComponent(heldItem.Value, out var ws) && ws.MoneyPerHitBonus > 0)
                payout += ws.MoneyPerHitBonus;

            if (_harvesterQuery.HasComponent(heldItem.Value))
                payout = (int) MathF.Round(payout * HarvesterMoneyMultiplier);
        }

        var give = Math.Min(payout, cap.MaxMoneyPerPlayer - alreadyGiven);
        cap.MoneyGivenPerPlayer[mindId] = alreadyGiven + give;
        _wallet.GiveCredits(mindId, give);
    }
}
