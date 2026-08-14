using Content.Shared._FinalStand.Placement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;

namespace Content.Server._FinalStand.Placement;

// Generic "Z toggles ghost placement mode, click a spot to confirm" plumbing shared by any FS placeable item.
public sealed partial class FSPlacementSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

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

        if (!_transform.InRange(Transform(args.User).Coordinates, args.ClickLocation, comp.Range))
            return;

        var ev = new FSPlacementConfirmedEvent(args.User, args.ClickLocation);
        RaiseLocalEvent(uid, ev);

        if (ev.Handled)
        {
            comp.Placing = false;
            Dirty(uid, comp);
        }
    }
}
