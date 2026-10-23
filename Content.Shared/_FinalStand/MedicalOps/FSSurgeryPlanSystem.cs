using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.MedicalOps;

public enum FSSurgeryNeed : byte
{
    NotNeeded,
    Active,
    Waiting,
}

public enum FSPlanGroupKind : byte
{
    Opening,
    Problem,
    Closing,
}

public enum FSPlanRowState : byte
{
    Done,
    Next,
    Ready,
    Waiting,
}

public sealed class FSSurgeryPlanRow
{
    public EntProtoId Surgery;
    public EntityUid SurgeryEnt;
    public EntProtoId Step;
    public EntityUid StepEnt;
    public FSPlanRowState State;
}

public sealed class FSSurgeryPlanGroup
{
    public FSPlanGroupKind Kind;
    public EntProtoId Surgery;
    public EntityUid SurgeryEnt;
    public LocId? Blocker;
    public bool Done;
    public readonly List<FSSurgeryPlanRow> Rows = new();
}

public sealed class FSSurgeryPlan
{
    public readonly List<FSSurgeryPlanGroup> Groups = new();
    public FSSurgeryPlanRow? Next;
    public bool IsOpen;
}

public sealed partial class FSSurgeryPlanSystem : EntitySystem
{
    [Dependency] private SharedSurgerySystem _surgery = default!;
    [Dependency] private FSSurgeryGuideSystem _guide = default!;

    public FSSurgeryNeed CheckNeed(EntityUid body, EntityUid part, EntityUid surgery, out LocId? blocker)
    {
        blocker = null;

        var ev = new SurgeryValidEvent(body, part);
        RaiseLocalEvent(surgery, ref ev);
        if (!ev.Cancelled)
            return FSSurgeryNeed.Active;

        if (ev.Blocker is not { } found)
            return FSSurgeryNeed.NotNeeded;

        var again = new SurgeryValidEvent(body, part, IgnoreBlockers: true);
        RaiseLocalEvent(surgery, ref again);
        if (again.Cancelled)
            return FSSurgeryNeed.NotNeeded;

        blocker = found;
        return FSSurgeryNeed.Waiting;
    }

    public FSSurgeryPlan BuildPlan(EntityUid body, EntityUid part)
    {
        return Build(body, part, _guide.Goals, false);
    }

    public FSSurgeryPlan BuildProcedure(EntityUid body, EntityUid part, EntProtoId surgery)
    {
        return Build(body, part, new[] { surgery }, true);
    }

    private FSSurgeryPlan Build(EntityUid body, EntityUid part, IEnumerable<EntProtoId> goals, bool keepFinished)
    {
        var plan = new FSSurgeryPlan();
        var openings = new List<EntProtoId>();
        var problems = new List<FSSurgeryPlanGroup>();
        var needs = new Dictionary<FSSurgeryPlanGroup, List<EntProtoId>>();

        foreach (var goalId in goals)
        {
            if (_surgery.GetSingleton(goalId) is not { } goal
                || !TryComp<SurgeryComponent>(goal, out var goalComp))
                continue;

            var need = CheckNeed(body, part, goal, out var blocker);
            if (need == FSSurgeryNeed.NotNeeded && !keepFinished)
                continue;

            var chain = RequirementChain(goalComp);
            foreach (var requirement in chain)
            {
                if (!openings.Contains(requirement))
                    openings.Add(requirement);
            }

            var group = NewGroup(body, part, FSPlanGroupKind.Problem, goalId, goal, goalComp);
            group.Blocker = blocker;

            if (need == FSSurgeryNeed.NotNeeded)
            {
                group.Done = true;
                foreach (var row in group.Rows)
                    row.State = FSPlanRowState.Done;
            }

            needs[group] = chain;
            problems.Add(group);
        }

        var openingGroups = new Dictionary<EntProtoId, FSSurgeryPlanGroup>();
        foreach (var openingId in openings)
        {
            if (_surgery.GetSingleton(openingId) is not { } opening
                || !TryComp<SurgeryComponent>(opening, out var openingComp))
                continue;

            var group = NewGroup(body, part, FSPlanGroupKind.Opening, openingId, opening, openingComp);
            openingGroups[openingId] = group;
            plan.Groups.Add(group);
        }

        plan.Groups.AddRange(problems);

        var closing = FindClosing(body, part);
        if (closing != null)
        {
            plan.IsOpen = !closing.Done;

            if (!plan.IsOpen && openingGroups.Count > 0)
            {
                closing.Done = false;
                foreach (var row in closing.Rows)
                    row.State = FSPlanRowState.Waiting;
            }

            if (plan.IsOpen || openingGroups.Count > 0)
                plan.Groups.Add(closing);
        }

        var next = PickNext(plan, openingGroups, needs);
        foreach (var group in plan.Groups)
            SetRowStates(group, next, IsActionable(group, openingGroups, needs, plan));

        plan.Next = next;
        return plan;
    }

    private FSSurgeryPlanRow? PickNext(FSSurgeryPlan plan,
        Dictionary<EntProtoId, FSSurgeryPlanGroup> openings,
        Dictionary<FSSurgeryPlanGroup, List<EntProtoId>> needs)
    {
        FSSurgeryPlanGroup? closing = null;

        foreach (var group in plan.Groups)
        {
            if (group.Kind == FSPlanGroupKind.Closing)
            {
                closing = group;
                continue;
            }

            if (IsActionable(group, openings, needs, plan) && FirstOpenRow(group) is { } row)
                return row;
        }

        if (closing == null || !plan.IsOpen)
            return null;

        foreach (var group in plan.Groups)
        {
            if (group.Kind == FSPlanGroupKind.Problem && !group.Done && group.Blocker == null)
                return null;
        }

        return FirstOpenRow(closing);
    }

    private bool IsActionable(FSSurgeryPlanGroup group,
        Dictionary<EntProtoId, FSSurgeryPlanGroup> openings,
        Dictionary<FSSurgeryPlanGroup, List<EntProtoId>> needs,
        FSSurgeryPlan plan)
    {
        if (group.Done)
            return false;

        switch (group.Kind)
        {
            case FSPlanGroupKind.Opening:
                return RequirementsDone(group.SurgeryEnt, openings);

            case FSPlanGroupKind.Problem:
                if (group.Blocker != null)
                    return false;

                foreach (var requirement in needs[group])
                {
                    if (openings.TryGetValue(requirement, out var opening) && !opening.Done)
                        return false;
                }

                return true;

            default:
                return plan.IsOpen;
        }
    }

    private bool RequirementsDone(EntityUid surgery, Dictionary<EntProtoId, FSSurgeryPlanGroup> openings)
    {
        if (!TryComp<SurgeryComponent>(surgery, out var comp))
            return true;

        foreach (var requirement in RequirementChain(comp))
        {
            if (openings.TryGetValue(requirement, out var opening) && !opening.Done)
                return false;
        }

        return true;
    }

    private static FSSurgeryPlanRow? FirstOpenRow(FSSurgeryPlanGroup group)
    {
        foreach (var row in group.Rows)
        {
            if (row.State != FSPlanRowState.Done)
                return row;
        }

        return null;
    }

    private static void SetRowStates(FSSurgeryPlanGroup group, FSSurgeryPlanRow? next, bool actionable)
    {
        var first = FirstOpenRow(group);

        foreach (var row in group.Rows)
        {
            if (row.State == FSPlanRowState.Done)
                continue;

            if (row == next)
                row.State = FSPlanRowState.Next;
            else if (row == first && actionable)
                row.State = FSPlanRowState.Ready;
            else
                row.State = FSPlanRowState.Waiting;
        }
    }

    private FSSurgeryPlanGroup NewGroup(EntityUid body,
        EntityUid part,
        FSPlanGroupKind kind,
        EntProtoId surgeryId,
        EntityUid surgery,
        SurgeryComponent comp)
    {
        var group = new FSSurgeryPlanGroup
        {
            Kind = kind,
            Surgery = surgeryId,
            SurgeryEnt = surgery,
        };

        var reachedOpenStep = false;
        foreach (var stepId in comp.Steps)
        {
            if (_surgery.GetSingleton(stepId) is not { } step)
                continue;

            var done = !reachedOpenStep && _surgery.IsStepComplete(body, part, stepId, surgery);
            if (!done)
                reachedOpenStep = true;

            group.Rows.Add(new FSSurgeryPlanRow
            {
                Surgery = surgeryId,
                SurgeryEnt = surgery,
                Step = stepId,
                StepEnt = step,
                State = done ? FSPlanRowState.Done : FSPlanRowState.Waiting,
            });
        }

        group.Done = !reachedOpenStep;
        return group;
    }

    private FSSurgeryPlanGroup? FindClosing(EntityUid body, EntityUid part)
    {
        foreach (var closingId in _guide.Closing)
        {
            if (_surgery.GetSingleton(closingId) is not { } closing
                || !TryComp<SurgeryComponent>(closing, out var closingComp)
                || CheckNeed(body, part, closing, out _) != FSSurgeryNeed.Active)
                continue;

            return NewGroup(body, part, FSPlanGroupKind.Closing, closingId, closing, closingComp);
        }

        return null;
    }

    public List<EntProtoId> RequirementChain(SurgeryComponent surgery)
    {
        var chain = new List<EntProtoId>();
        var current = surgery;

        while (current.Requirement is { } requirementId
               && !chain.Contains(requirementId)
               && _surgery.GetSingleton(requirementId) is { } requirement
               && TryComp<SurgeryComponent>(requirement, out var requirementComp))
        {
            chain.Insert(0, requirementId);
            current = requirementComp;
        }

        return chain;
    }
}
