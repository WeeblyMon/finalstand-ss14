// Vanilla TargetedProjectileComponent.Target is a non-nullable EntityUid checked only when the
// shot is fired. If the target dies while the bullet is still travelling, every PVS serialization
// of that bullet throws in GetNetEntity — once per client per tick, each with a stack-trace
// capture, inside the parallel serialize loop. A wave game kills targets mid-flight constantly.
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server._FinalStand.Projectiles;

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
