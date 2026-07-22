using Content.Server._FinalStand.Perks;
using Content.Server._FinalStand.Economy;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Visuals;

public sealed class FSZombieVisualsSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly FSPlayerWalletSystem _wallet = default!;

    private const int ProfiteerKillBase = 200;
    private const float ProfiteerFraction = 0.07f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieVisualsComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<FSZombieVisualsComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnDamageChanged(EntityUid uid, FSZombieVisualsComponent comp, DamageChangedEvent args)
    {
        if (!TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return;

        var deathThreshold = 0f;
        foreach (var (threshold, state) in thresholds.Thresholds)
        {
            if (state == MobState.Dead)
                deathThreshold = threshold.Float();
        }

        if (deathThreshold <= 0f)
            return;

        var currentDamage = _damageable.GetTotalDamage((uid, args.Damageable)).Float();
        var healthPercent = 1f - currentDamage / deathThreshold;

        var newStage = healthPercent switch
        {
            >= 0.8f => 0,
            >= 0.6f => 1,
            >= 0.4f => 2,
            >= 0.2f => 3,
            _       => 4,
        };

        // Pick the random alt once when reaching the final damage stage.
        if (newStage == 4 && !comp.AltPicked)
        {
            comp.DeathAlt = _random.Next(0, 3);
            comp.AltPicked = true;
        }

        if (comp.DamageStage == newStage)
            return;

        comp.DamageStage = newStage;
        Dirty(uid, comp);
    }

    private void OnMobStateChanged(EntityUid uid, FSZombieVisualsComponent comp, MobStateChangedEvent args)
    {
        Dirty(uid, comp);

        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead) return;
        if (args.Origin == null) return;
        if (!_mind.TryGetMind(args.Origin.Value, out var mindId, out _)) return;
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var augs)) return;
        var level = augs.GetSlottedLevel("Profiteer");
        if (level <= 0) return;
        _wallet.GiveCredits(mindId, (int)(ProfiteerKillBase * level * ProfiteerFraction));
    }
}
