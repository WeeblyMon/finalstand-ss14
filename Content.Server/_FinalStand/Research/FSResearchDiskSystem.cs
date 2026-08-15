using Content.Server.Popups;
using Content.Shared._FinalStand.Research;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Research.Components;

namespace Content.Server._FinalStand.Research;

public sealed partial class FSResearchDiskSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private FSResearchSystem _research = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSResearchDiskComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(EntityUid uid, FSResearchDiskComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (!HasComp<ResearchServerComponent>(args.Target))
            return;

        EntityUid? contributorMindId = _mind.TryGetMind(args.User, out var mindId, out _) ? mindId : null;
        _research.GrantResearchPoints(comp.Points, "research-disk", contributorMindId);
        _popup.PopupEntity(Loc.GetString("fs-research-disk-inserted", ("points", comp.Points)), args.Target!.Value, args.User);
        QueueDel(uid);
        args.Handled = true;
    }
}
