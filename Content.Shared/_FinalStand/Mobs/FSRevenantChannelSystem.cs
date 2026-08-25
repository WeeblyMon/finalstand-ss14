using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Events;

namespace Content.Shared._FinalStand.Mobs;

public sealed partial class FSRevenantChannelSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSRevenantChannelComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSRevenantChannelComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FSRevenantChannelComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
    }

    private void OnStartup(Entity<FSRevenantChannelComponent> ent, ref ComponentStartup args)
    {
        _blocker.UpdateCanMove(ent);
    }

    private void OnShutdown(Entity<FSRevenantChannelComponent> ent, ref ComponentShutdown args)
    {
        _blocker.UpdateCanMove(ent);
    }

    private void OnUpdateCanMove(Entity<FSRevenantChannelComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.Cancel();
    }
}
