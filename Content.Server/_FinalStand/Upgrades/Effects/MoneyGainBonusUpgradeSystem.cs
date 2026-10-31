using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed partial class MoneyGainBonusUpgradeSystem : EntitySystem
{
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSWaveRewardsSystem _rewards = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
        SubscribeLocalEvent<FSPendingKillBonusComponent, MobStateChangedEvent>(OnEnemyDied);
    }

    private void OnHit(ref FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (ev.State is not { } state)
            return;

        Mark(ev.Target, ev.Shooter, state.MoneyGainBonusPerKill, state.MoneyGainPercentPerKill);
    }

    // The latest hitter's weapon decides the bonus, and only they collect it.
    public void Mark(EntityUid target, EntityUid? owner, int bonus, float percent)
    {
        if ((bonus <= 0 && percent <= 0f) || owner == null || !HasComp<WaveSpawnedTagComponent>(target))
            return;

        var pending = EnsureComp<FSPendingKillBonusComponent>(target);
        pending.MoneyBonus = bonus;
        pending.MoneyPercent = percent;
        pending.Payee = owner;
    }

    private void OnEnemyDied(EntityUid uid, FSPendingKillBonusComponent bonus, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;
        if (args.Origin is not { } killer || killer != bonus.Payee || !_mind.TryGetMind(killer, out var mindId, out _))
            return;

        var credits = bonus.MoneyBonus + (int) MathF.Round(bonus.MoneyPercent * _rewards.KillCredits(uid));
        if (credits > 0)
            _wallet.GiveCredits(mindId, credits);
    }
}
