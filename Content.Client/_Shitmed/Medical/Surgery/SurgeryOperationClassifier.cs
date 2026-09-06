// FINALSTAND: what an operation treats, and how badly it wants doing.
//
// Both answers come from the conditions a surgery is gated on rather than a hardcoded list of
// prototype ids: a procedure that only exists while something is wrong is, by construction, the one
// that needs doing. New surgeries classify themselves.

using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;

namespace Content.Client._Shitmed.Medical.Surgery;

public enum SurgeryFocus : byte
{
    All,
    Bleeding,
    Wounds,
    Bones,
    Organs,
}

public sealed class SurgeryOperationClassifier
{
    // Lower runs first when no focus is chosen.
    public const int UrgencyBleeding = 0;
    public const int UrgencyTrauma = 1;
    public const int UrgencyAccess = 2;
    public const int UrgencyClosing = 3;
    public const int UrgencyElective = 4;

    private readonly IEntityManager _entities;

    public SurgeryOperationClassifier(IEntityManager entities)
    {
        _entities = entities;
    }

    /// <summary>
    /// Closing ranks above elective work. Organ inserts are permanently available on an empty slot,
    /// so ranking them first meant the surgeon was never once told to close the patient back up.
    /// </summary>
    public int UrgencyOf(EntityUid surgery)
    {
        if (_entities.HasComponent<SurgeryCloseIncisionConditionComponent>(surgery))
            return UrgencyClosing;

        if (IsBleedingWork(surgery))
            return UrgencyBleeding;

        if (_entities.HasComponent<SurgeryWoundedConditionComponent>(surgery) || TraumaOf(surgery) != null)
            return UrgencyTrauma;

        if (_entities.HasComponent<SurgeryOperatingTableConditionComponent>(surgery))
            return UrgencyAccess;

        return UrgencyElective;
    }

    public bool Matches(EntityUid surgery, SurgeryFocus focus)
    {
        return focus == SurgeryFocus.All || FocusOf(surgery) == focus;
    }

    private SurgeryFocus? FocusOf(EntityUid surgery)
    {
        if (IsBleedingWork(surgery))
            return SurgeryFocus.Bleeding;

        if (_entities.HasComponent<SurgeryWoundedConditionComponent>(surgery))
            return SurgeryFocus.Wounds;

        if (TraumaOf(surgery) is { } trauma)
        {
            return trauma switch
            {
                TraumaType.BoneDamage => SurgeryFocus.Bones,
                TraumaType.Dismemberment => SurgeryFocus.Bones,
                TraumaType.OrganDamage => SurgeryFocus.Organs,
                TraumaType.VeinsDamage => SurgeryFocus.Bleeding,
                _ => SurgeryFocus.Wounds,
            };
        }

        // Inserting and removing organs is elective, but it is still organ work.
        return _entities.HasComponent<SurgeryOrganConditionComponent>(surgery)
            ? SurgeryFocus.Organs
            : null;
    }

    private bool IsBleedingWork(EntityUid surgery)
    {
        return _entities.TryGetComponent<SurgeryBleedsPresentConditionComponent>(surgery, out var bleeds)
               && !bleeds.Inverted;
    }

    private TraumaType? TraumaOf(EntityUid surgery)
    {
        return _entities.TryGetComponent<SurgeryTraumaPresentConditionComponent>(surgery, out var trauma)
               && !trauma.Inverted
            ? trauma.TraumaType
            : null;
    }
}
