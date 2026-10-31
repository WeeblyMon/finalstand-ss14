using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server._FinalStand.Projectiles;

// Vanilla never clears a TargetedProjectile target that died mid-flight, and every PVS send of the bullet then throws.
public sealed class FSStaleProjectileTargetSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TargetedProjectileComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
#pragma warning disable RA0002
            var target = comp.Target;
#pragma warning restore RA0002

            if (target.IsValid() && !TerminatingOrDeleted(target))
                continue;

            RemCompDeferred<TargetedProjectileComponent>(uid);
        }
    }
}
