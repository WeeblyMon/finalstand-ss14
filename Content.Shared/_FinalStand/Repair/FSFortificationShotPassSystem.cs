using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;

namespace Content.Shared._FinalStand.Repair;

public sealed partial class FSFortificationShotPassSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSPlateRepairComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<FSPlateRepairComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled
            || HasComp<FSRivetComponent>(args.OtherEntity)
            || !TryComp<ProjectileComponent>(args.OtherEntity, out var projectile)
            || projectile.Shooter is not { } shooter
            || !HasComp<FSFriendlyFireComponent>(shooter))
            return;

        args.Cancelled = true;
    }
}
