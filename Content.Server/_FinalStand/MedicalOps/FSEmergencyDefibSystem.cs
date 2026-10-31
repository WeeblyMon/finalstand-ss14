using Content.Shared._FinalStand.Ailments;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Traits.Assorted;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSEmergencyDefibSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private FSMedicalUpgradeSystem _upgrades = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedRottingSystem _rotting = default!;

    // The vanilla zap revives before TargetDefibrillatedEvent, so whether the patient was dead is noted just before it.
    // Set fresh on every zap, so one that gets cancelled after the check can't leave a living patient marked dead.
    private EntityUid? _zappedWhileDead;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSAilmentStateComponent, TargetBeforeDefibrillatorZapsEvent>(OnBeforeZap);
        SubscribeLocalEvent<DamageableComponent, TargetDefibrillatedEvent>(OnDefibrillated);
    }

    private void OnBeforeZap(EntityUid patient, FSAilmentStateComponent state, TargetBeforeDefibrillatorZapsEvent args)
    {
        _zappedWhileDead = _mobState.IsDead(patient) ? patient : null;
    }

    private void OnDefibrillated(EntityUid patient, DamageableComponent damageable, ref TargetDefibrillatedEvent args)
    {
        var wasDead = _zappedWhileDead == patient;
        _zappedWhileDead = null;

        if (!TryComp<FSEmergencyDefibComponent>(args.Defibrillator.Owner, out var emergency))
            return;

        if (wasDead || _mobState.IsDead(patient))
            Revive(patient, damageable, emergency, args.User);
        else if (_mobState.IsCritical(patient))
            StabilizeToward(patient, damageable, MobState.Critical, emergency.ReviveHealthFraction, args.User);
    }

    private void Revive(EntityUid patient, DamageableComponent damageable, FSEmergencyDefibComponent emergency, EntityUid medic)
    {
        if (_rotting.IsRotten(patient) || HasComp<UnrevivableComponent>(patient))
            return;

        StabilizeToward(patient, damageable, MobState.Dead, emergency.ReviveHealthFraction, medic);
        if (_mobState.IsDead(patient)
            && _thresholds.TryGetThresholdForState(patient, MobState.Dead, out var dead)
            && _damageable.GetTotalDamage((patient, damageable)) < dead)
        {
            _mobState.ChangeMobState(patient, MobState.Critical, origin: medic);
        }

        if (_mobState.IsDead(patient) || !_upgrades.Unlocked(FSMedicalUpgradeSystem.RestartProtocol))
            return;

        _ailments.Cure(patient, FSAilmentSlot.Bone, medic, FSCureKind.Rider);
        _ailments.Cure(patient, FSAilmentSlot.Burn, medic, FSCureKind.Rider);
    }

    // The medic who used the defib is the heal's origin, so it's credited to them rather than whoever treated last.
    private void StabilizeToward(EntityUid patient, DamageableComponent damageable, MobState state, float reviveHealthFraction,
        EntityUid medic)
    {
        if (!_thresholds.TryGetThresholdForState(patient, state, out var threshold)
            || threshold is not { } stateThreshold
            || stateThreshold <= 0)
            return;

        var target = stateThreshold.Float() * (1f - reviveHealthFraction);
        var current = (float)_damageable.GetTotalDamage((patient, damageable));

        if (current <= target)
            return;

        var heal = new DamageSpecifier(damageable.Damage) * -((current - target) / current);
        _damageable.TryChangeDamage(patient, heal, ignoreResistances: true, origin: medic);
    }
}
