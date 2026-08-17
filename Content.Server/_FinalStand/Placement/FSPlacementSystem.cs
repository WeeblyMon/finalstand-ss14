// Generic "use item toggles ghost placement mode, click a spot to confirm" plumbing shared by any FS placeable item.
using Content.Shared._FinalStand.Placement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;

namespace Content.Server._FinalStand.Placement;

public sealed class FSPlacementSystem : EntitySystem
{
    [Dependency] private FSPlacementRuleSystem _rule = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSPlaceableComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<FSPlaceableComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnUse(EntityUid uid, FSPlaceableComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        comp.Placing = !comp.Placing;
        Dirty(uid, comp);
        args.Handled = true;
    }

    private void OnAfterInteract(EntityUid uid, FSPlaceableComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !comp.Placing)
            return;

        args.Handled = true;

        if (!_rule.CanPlaceAt(args.User, args.ClickLocation, comp))
            return;

        var ev = new FSPlacementConfirmedEvent(args.User, args.ClickLocation);
        RaiseLocalEvent(uid, ev);

        if (!ev.Handled)
            return;

        comp.Placing = false;
        Dirty(uid, comp);
    }
}
