using Content.Server._FinalStand.Economy;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed class MoneyGainBonusUpgradeSystem : EntitySystem
{
    [Dependency] private readonly FSPlayerWalletSystem _wallet = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
        SubscribeLocalEvent<FSPendingKillBonusComponent, MobStateChangedEvent>(OnEnemyDied);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(ev.Weapon.Value, out var state) || state.MoneyGainBonusPerKill <= 0)
            return;

        var bonus = EnsureComp<FSPendingKillBonusComponent>(ev.Target);
        bonus.MoneyBonus = state.MoneyGainBonusPerKill;
    }

    private void OnEnemyDied(EntityUid uid, FSPendingKillBonusComponent bonus, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;
        if (bonus.MoneyBonus <= 0)
            return;
        if (args.Origin == null || !_mind.TryGetMind(args.Origin.Value, out var mindId, out var mindComp))
            return;

        _wallet.GiveCredits(mindId, bonus.MoneyBonus);
    }
}
