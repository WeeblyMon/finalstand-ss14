using Content.Shared._FinalStand.Ailments;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSEmergencyDefibSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private FSMedicalUpgradeSystem _upgrades = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;

    private readonly HashSet<EntityUid> _wasDead = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSAilmentStateComponent, TargetBeforeDefibrillatorZapsEvent>(OnBeforeZap);
        SubscribeLocalEvent<DamageableComponent, TargetDefibrillatedEvent>(OnDefibrillated);
    }

    private void OnBeforeZap(EntityUid patient, FSAilmentStateComponent state, TargetBeforeDefibrillatorZapsEvent args)
    {
        if (_mobState.IsDead(patient))
            _wasDead.Add(patient);
    }

    private void OnDefibrillated(EntityUid patient, DamageableComponent damageable, ref TargetDefibrillatedEvent args)
    {
        var wasDead = _wasDead.Remove(patient);
        if (!TryComp<FSEmergencyDefibComponent>(args.Defibrillator.Owner, out var emergency))
            return;

        if (wasDead || _mobState.IsDead(patient))
        {
            Revive(patient, damageable, emergency, args.User);
            return;
        }

        if (_mobState.IsCritical(patient))
            StabilizeToward(patient, damageable, MobState.Critical, emergency.ReviveHealthFraction);
    }

    private void Revive(EntityUid patient, DamageableComponent damageable, FSEmergencyDefibComponent emergency, EntityUid user)
    {
        StabilizeToward(patient, damageable, MobState.Dead, emergency.ReviveHealthFraction);
        if (_mobState.IsDead(patient)
            && _thresholds.TryGetThresholdForState(patient, MobState.Dead, out var dead)
            && _damageable.GetTotalDamage((patient, damageable)) < dead)
        {
            _mobState.ChangeMobState(patient, MobState.Critical, origin: user);
        }

        if (_mobState.IsDead(patient) || !_upgrades.Unlocked(FSMedicalUpgradeSystem.RestartProtocol))
            return;

        _ailments.Cure(patient, FSAilmentSlot.Bone, user, FSCureKind.Rider);
        _ailments.Cure(patient, FSAilmentSlot.Burn, user, FSCureKind.Rider);
    }

    private void StabilizeToward(EntityUid patient, DamageableComponent damageable, MobState state, float reviveHealthFraction)
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
        _damageable.TryChangeDamage(patient, heal, ignoreResistances: true);
    }
}
