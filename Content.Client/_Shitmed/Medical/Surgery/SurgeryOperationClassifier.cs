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
    public const int UrgencyBleeding = 100;
    public const int UrgencyTrauma = 101;
    public const int UrgencyAccess = 102;
    public const int UrgencyClosing = 103;
    public const int UrgencyElective = 104;

    private static readonly Dictionary<string, int> ExplicitOrder = new()
    {
        ["SurgeryOpenIncision"] = 0,
        ["SurgeryOpenRibcage"] = 1,
        ["SurgeryStopBloodOutput"] = 2,
        ["SurgeryFixDismemberment"] = 3,
        ["SurgeryMendBones"] = 4,
        ["SurgeryHealOrgans"] = 5,
        ["SurgeryTendWoundsBrute"] = 6,
        ["SurgeryTendWoundsBurn"] = 7,
        ["SurgeryMendBrainTissue"] = 8,
        ["SurgeryCloseIncision"] = 90,
        ["SurgeryCloseIncisionHead"] = 90,
        ["SurgeryCloseIncisionChest"] = 90,
    };

    private readonly IEntityManager _entities;

    public SurgeryOperationClassifier(IEntityManager entities)
    {
        _entities = entities;
    }

    public int UrgencyOf(EntityUid surgery, string? protoId = null)
    {
        if (protoId != null && ExplicitOrder.TryGetValue(protoId, out var explicitRank))
            return explicitRank;

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
