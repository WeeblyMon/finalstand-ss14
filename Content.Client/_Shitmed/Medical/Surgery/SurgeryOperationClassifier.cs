using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Robust.Shared.Prototypes;

namespace Content.Client._Shitmed.Medical.Surgery;

public enum SurgeryFocus : byte
{
    All,
    Bleeding,
    Wounds,
    Bones,
    Organs,
}

public enum SurgeryRole : byte
{
    Goal,
    Access,
    Closing,
    Elective,
}

public sealed class SurgeryOperationClassifier
{
    public const int UrgencyElective = 1000;
    public const int UrgencyClosing = 2000;
    public const int UrgencyAccess = 3000;

    private readonly IEntityManager _entities;
    private readonly IPrototypeManager _prototypes;

    private readonly Dictionary<string, int> _goalOrder = new();
    private readonly HashSet<string> _access = new();
    private readonly HashSet<string> _closing = new();

    public SurgeryOperationClassifier(IEntityManager entities, IPrototypeManager prototypes)
    {
        _entities = entities;
        _prototypes = prototypes;

        LoadGuide();
    }

    private void LoadGuide()
    {
        foreach (var guide in _prototypes.EnumeratePrototypes<FSSurgeryGuidePrototype>())
        {
            for (var i = 0; i < guide.Goals.Count; i++)
                _goalOrder.TryAdd(guide.Goals[i].Id, i);

            foreach (var id in guide.Access)
                _access.Add(id.Id);

            foreach (var id in guide.Closing)
                _closing.Add(id.Id);
        }
    }

    public SurgeryRole RoleOf(string? protoId)
    {
        if (protoId == null)
            return SurgeryRole.Elective;

        if (_goalOrder.ContainsKey(protoId))
            return SurgeryRole.Goal;

        if (_access.Contains(protoId))
            return SurgeryRole.Access;

        return _closing.Contains(protoId) ? SurgeryRole.Closing : SurgeryRole.Elective;
    }

    public bool IsAccess(string? protoId) => protoId != null && _access.Contains(protoId);

    public int UrgencyOf(EntityUid surgery, string? protoId = null)
    {
        if (protoId != null && _goalOrder.TryGetValue(protoId, out var authored))
            return authored;

        if (IsAccess(protoId))
            return UrgencyAccess;

        if (protoId != null && _closing.Contains(protoId))
            return UrgencyClosing;

        if (_entities.HasComponent<SurgeryCloseIncisionConditionComponent>(surgery))
            return UrgencyClosing;

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
