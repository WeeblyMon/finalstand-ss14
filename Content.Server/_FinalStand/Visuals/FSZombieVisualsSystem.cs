using Content.Shared._FinalStand.Visuals;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Visuals;

public sealed partial class FSZombieVisualsSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieVisualsComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<FSZombieVisualsComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnDamageChanged(EntityUid uid, FSZombieVisualsComponent comp, DamageChangedEvent args)
    {
        if (!_thresholds.TryGetThresholdForState(uid, MobState.Dead, out var deadAt))
            return;

        var deathThreshold = deadAt.Value.Float();
        if (deathThreshold <= 0f)
            return;

        var currentDamage = _damageable.GetTotalDamage((uid, args.Damageable)).Float();
        var healthPercent = 1f - currentDamage / deathThreshold;

        var newStage = comp.SingleStageAt >= 0f
            ? (healthPercent > comp.SingleStageAt ? 0 : 1)
            : healthPercent switch
            {
                >= 0.8f => 0,
                >= 0.6f => 1,
                >= 0.4f => 2,
                >= 0.2f => 3,
                _       => 4,
            };

        var changed = false;

        if (newStage == 4 && !comp.AltPicked)
        {
            comp.DeathAlt = _random.Next(0, 3);
            comp.AltPicked = true;
            changed = true;
        }

        if (comp.DamageStage != newStage)
        {
            comp.DamageStage = newStage;
            changed = true;
        }

        if (changed)
            Dirty(uid, comp);
    }

    private void OnMobStateChanged(EntityUid uid, FSZombieVisualsComponent comp, MobStateChangedEvent args)
    {
        Dirty(uid, comp);
    }
}
