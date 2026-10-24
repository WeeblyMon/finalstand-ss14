using Content.Shared._FinalStand.Medical;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.CombatMode;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSSurgeryToolUseSystem : EntitySystem
{
    [Dependency] private SharedSurgerySystem _surgery = default!;
    [Dependency] private FSSurgeryPlanSystem _plan = default!;
    [Dependency] private OrganLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;

    private static readonly ProtoId<TagPrototype> SurgeryToolTag = "SurgeryTool";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryToolComponent, AfterInteractEvent>(OnAfterInteract, before: [typeof(IngestionSystem)]);
    }

    private void OnAfterInteract(Entity<SurgeryToolComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } body || body == args.User)
            return;

        var user = args.User;
        if (!HasComp<SurgeryTargetComponent>(body)
            || !_surgery.IsLaidOut(body)
            || _combat.IsInCombatMode(user)
            || !TryComp<SurgeryTargetComponent>(user, out var surgeon)
            || !surgeon.CanOperate)
            return;

        var windowOpen = _ui.IsUiOpen(body, SurgeryUIKey.Key, user);
        var surgical = _tags.HasTag(ent, SurgeryToolTag);
        if (!windowOpen && !surgical)
            return;

        if (FindStep(body, user, ent) is { } found)
        {
            args.Handled = true;

            if (!_surgery.TryDoSurgeryStep(body, found.Part, user, found.Row.Surgery, found.Row.Step, out var reason, out var popup))
                _popup.PopupClient(_surgery.DescribeRefusal(reason, found.Row.Step, popup), user, user, PopupType.SmallCaution);

            return;
        }

        if (!surgical)
            return;

        args.Handled = true;

        if (!windowOpen)
        {
            _surgery.TryOpenSurgery(user, body);
            return;
        }

        if (NextAnywhere(body, user) is { } next)
        {
            _popup.PopupClient(Loc.GetString("fs-surgery-wrong-tool",
                    ("step", Name(next.StepEnt)),
                    ("tool", string.Join(", ", _surgery.GetStepToolNames(next.StepEnt)))),
                user,
                user,
                PopupType.SmallCaution);
            return;
        }

        _popup.PopupClient(Loc.GetString("fs-surgery-nothing-to-do"), user, user, PopupType.Small);
    }

    private (EntityUid Part, FSSurgeryPlanRow Row)? FindStep(EntityUid body, EntityUid user, EntityUid tool)
    {
        foreach (var part in Candidates(body, user))
        {
            if (_plan.BuildPlan(body, part).Next is { } next
                && _surgery.ToolFitsStep(user, body, part, next.StepEnt, tool))
                return (part, next);
        }

        return null;
    }

    private FSSurgeryPlanRow? NextAnywhere(EntityUid body, EntityUid user)
    {
        foreach (var part in Candidates(body, user))
        {
            if (_plan.BuildPlan(body, part).Next is { } next)
                return next;
        }

        return null;
    }

    private IEnumerable<EntityUid> Candidates(EntityUid body, EntityUid user)
    {
        EntityUid? targeted = null;

        if (TryComp<TargetingComponent>(user, out var targeting))
        {
            foreach (var category in OrganCategories.FromTarget(targeting.Target))
            {
                foreach (var organ in _lookup.EnumerateOrgansOfCategory(body, category))
                {
                    targeted = organ.Owner;
                    break;
                }

                if (targeted != null)
                    break;
            }
        }

        if (targeted is { } first)
            yield return first;

        foreach (var organ in _lookup.GetBodyOrgans(body))
        {
            if (organ.Owner != targeted && OrganCategories.ToTarget(organ.Comp.Category) != null)
                yield return organ.Owner;
        }
    }

    private string Name(EntityUid uid)
    {
        return MetaData(uid).EntityName;
    }
}
