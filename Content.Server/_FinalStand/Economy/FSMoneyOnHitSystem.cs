using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.Weapons;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;

namespace Content.Server._FinalStand.Economy;

public sealed class FSMoneyOnHitSystem : EntitySystem
{
    [Dependency] private readonly FSPlayerWalletSystem _wallet = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private EntityQuery<FSWeaponUpgradeStateComponent> _upgradeQuery;
    private EntityQuery<FSArmorComponent> _armorQuery;
    private EntityQuery<FSHarvesterComponent> _harvesterQuery;

    private const int BaseMoneyPerHit = 30;

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
    }

    private void OnDamageChanged(EntityUid uid, FSMoneyOnHitCapComponent cap, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin == null)
            return;
        if (_armorQuery.TryGetComponent(uid, out var armor) && armor.CurrentArmor > 0)
            return;

        if (!_mind.TryGetMind(args.Origin.Value, out var mindId, out _))
            return;

        cap.MoneyGivenPerPlayer.TryGetValue(mindId, out var alreadyGiven);
        if (alreadyGiven >= cap.MaxMoneyPerPlayer)
            return;

        var payout = BaseMoneyPerHit;
        if (_hands.TryGetActiveItem(args.Origin.Value, out var heldItem))
        {
            if (_upgradeQuery.TryGetComponent(heldItem.Value, out var ws) && ws.MoneyPerHitBonus > 0)
                payout += ws.MoneyPerHitBonus;

            if (_harvesterQuery.HasComponent(heldItem.Value))
                payout = (int) MathF.Round(payout * HarvesterMoneyMultiplier);
        }

        var give = Math.Min(payout, cap.MaxMoneyPerPlayer - alreadyGiven);
        cap.MoneyGivenPerPlayer[mindId] = alreadyGiven + give;
        _wallet.GiveCredits(mindId, give);
    }
}
