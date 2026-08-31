using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Server._FinalStand.MedicalOps;

// Vanilla defibrillation leaves the patient in Critical. The emergency unit carries them the rest
// of the way up, on a sliver of health, so a Combat Medic can get someone fighting again mid-wave.
public sealed partial class FSEmergencyDefibSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageableComponent, TargetDefibrillatedEvent>(OnDefibrillated);
    }

    private void OnDefibrillated(EntityUid patient, DamageableComponent damageable, ref TargetDefibrillatedEvent args)
    {
        if (!TryComp<FSEmergencyDefibComponent>(args.Defibrillator.Owner, out var emergency))
            return;

        // The event fires even when the zap failed, so only act once the patient is actually back.
        if (!_mobState.IsCritical(patient))
            return;

        if (!_thresholds.TryGetThresholdForState(patient, MobState.Critical, out var threshold)
            || threshold is not { } critThreshold
            || critThreshold <= 0)
            return;

        var target = critThreshold.Float() * (1f - emergency.ReviveHealthFraction);
        var current = (float)_damageable.GetTotalDamage((patient, damageable));

        if (current <= target)
            return;

        // Scale their existing damage down proportionally rather than clearing types outright, so
        // the wound they were killed by is reduced, not erased.
        var heal = new DamageSpecifier(damageable.Damage) * -((current - target) / current);
        _damageable.TryChangeDamage(patient, heal, ignoreResistances: true, origin: args.User);
    }
}
