using Content.Shared.Body.Events;
using Content.Shared.Hands.EntitySystems;

namespace Content.Shared.Body;

public sealed class HandOrganSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandOrganComponent, OrganAddedToBodyEvent>(OnGotInserted);
        SubscribeLocalEvent<HandOrganComponent, OrganRemovedFromBodyEvent>(OnGotRemoved);
    }

    private void OnGotInserted(Entity<HandOrganComponent> ent, ref OrganAddedToBodyEvent args)
    {
        _hands.AddHand(args.Body, ent.Comp.HandID, ent.Comp.Data);
    }

    private void OnGotRemoved(Entity<HandOrganComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        // prevent a recursive double-delete bug
        if (LifeStage(args.OldBody) >= EntityLifeStage.Terminating)
            return;

        _hands.RemoveHand(args.OldBody, ent.Comp.HandID);
    }
}
