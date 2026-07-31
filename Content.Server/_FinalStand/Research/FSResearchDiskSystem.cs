using Content.Server.Popups;
using Content.Shared._FinalStand.Research;
using Content.Shared.Interaction;
using Content.Shared.Research.Components;

namespace Content.Server._FinalStand.Research;

public sealed class FSResearchDiskSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly FSResearchSystem _research = default!;

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

        _research.GrantResearchPoints(comp.Points, "research-disk");
        _popup.PopupEntity(Loc.GetString("fs-research-disk-inserted", ("points", comp.Points)), args.Target!.Value, args.User);
        QueueDel(uid);
        args.Handled = true;
    }
}
