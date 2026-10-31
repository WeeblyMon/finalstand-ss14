using Content.Shared._FinalStand.MedicalOps;

namespace Content.Shared._FinalStand.Ailments;

public sealed partial class FSAilmentCureSystem : EntitySystem
{
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private FSMedicalBonusSystem _medicalBonus = default!;

    public bool Cures(EntityUid item, EntityUid patient)
    {
        return TryComp<FSAilmentCureComponent>(item, out var cure) && _ailments.Has(patient, cure.Slot);
    }

    public void Treated(EntityUid item, EntityUid patient, EntityUid user, float stoppedBleed)
    {
        if (!TryComp<FSAilmentCureComponent>(item, out var cure))
            return;

        if (cure.Ward is { } ward)
            _ailments.ApplyWard(patient, ward);

        if (cure.Mend > 0f)
            _ailments.AddMend(patient, cure.Slot, cure.Mend, user, FSCureKind.HandsOn);
        else if (cure.Slot != FSAilmentSlot.Bleeding)
            _ailments.Cure(patient, cure.Slot, user);
        else if (stoppedBleed > 0f)
            _ailments.ReportBleedStopped(patient, stoppedBleed, user);
    }

    public TimeSpan GetTreatmentDelay(EntityUid user, EntityUid patient, TimeSpan delay)
    {
        delay *= _medicalBonus.GetDelayMultiplier(user, FSMedicalBonusCategory.TreatmentSpeed);
        if (user != patient && TryComp<FSSteadyHandsComponent>(user, out var hands))
            delay *= hands.DelayMultiplier;

        return delay;
    }
}
