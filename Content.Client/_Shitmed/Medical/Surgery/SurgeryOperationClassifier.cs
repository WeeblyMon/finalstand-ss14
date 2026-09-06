// FINALSTAND: classifies an operation by the conditions it is gated on.

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
        if (focus == SurgeryFocus.All)
            return true;

        var kind = FocusOf(surgery);
        return kind == null || kind == focus;
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
