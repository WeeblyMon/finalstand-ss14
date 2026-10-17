using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Events;

namespace Content.Shared._FinalStand.Upgrades.Effects;

// Runs on both sides so movement prediction agrees during a knockback.
public sealed partial class FSKnockedBackSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSKnockedBackComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSKnockedBackComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FSKnockedBackComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
    }

    private void OnStartup(Entity<FSKnockedBackComponent> ent, ref ComponentStartup args)
    {
        _blocker.UpdateCanMove(ent);
    }

    private void OnShutdown(Entity<FSKnockedBackComponent> ent, ref ComponentShutdown args)
    {
        _blocker.UpdateCanMove(ent);
    }

    private void OnUpdateCanMove(Entity<FSKnockedBackComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        if (TryComp<FSKnockbackResistComponent>(ent, out var resist) && !resist.LocksMovement)
            return;

        args.Cancel();
    }
}
