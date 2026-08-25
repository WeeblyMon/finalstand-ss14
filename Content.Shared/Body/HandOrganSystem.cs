using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Shared.Body;

public sealed partial class HandOrganSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandOrganComponent, OrganGotInsertedEvent>(OnGotInserted);
        SubscribeLocalEvent<HandOrganComponent, OrganGotRemovedEvent>(OnGotRemoved);
    }

    private void OnGotInserted(Entity<HandOrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        // FINALSTAND: hands are rebuilt from the incoming state, so reacting here fights it.
        if (_timing.ApplyingState)
            return;

        _hands.AddHand(args.Target, ent.Comp.HandID, ent.Comp.Data);
    }

    private void OnGotRemoved(Entity<HandOrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        // FINALSTAND: a client state reset detaches the body to nullspace without terminating it,
        // so the guard below misses and dropping held items warns about having no grid to drop to.
        if (_timing.ApplyingState)
            return;

        // prevent a recursive double-delete bug
        if (LifeStage(args.Target) >= EntityLifeStage.Terminating)
            return;

        _hands.RemoveHand(args.Target, ent.Comp.HandID);
    }
}
