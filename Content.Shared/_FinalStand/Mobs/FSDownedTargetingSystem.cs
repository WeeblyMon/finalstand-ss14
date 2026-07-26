using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Standing;

namespace Content.Shared._FinalStand.Mobs;

// Vanilla RequireProjectileTargetComponent stops stray fire from hitting a downed ally unless you
// explicitly target them. Wave enemies shouldn't get that protection when flashbanged/knocked down —
// it made stunned zombies require pixel-precise clicks and blocked pierce from hitting them at all.
public sealed class FSDownedTargetingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSWaveDamageScaleComponent, DownedEvent>(OnEnemyDowned, after: [typeof(RequireProjectileTargetSystem)]);
    }

    private void OnEnemyDowned(EntityUid uid, FSWaveDamageScaleComponent _, DownedEvent args)
    {
        if (!TryComp<RequireProjectileTargetComponent>(uid, out var reqTarget) || !reqTarget.Active)
            return;

#pragma warning disable RA0002
        reqTarget.Active = false;
#pragma warning restore RA0002
        Dirty(uid, reqTarget);
    }
}
