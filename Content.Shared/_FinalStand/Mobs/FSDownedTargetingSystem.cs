using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Standing;

namespace Content.Shared._FinalStand.Mobs;

// Vanilla RequireProjectileTargetComponent blocks stray fire on downed allies unless explicitly targeted;
// wave enemies shouldn't get that protection when knocked down. Keyed on FSWaveDamageScaleComponent
// (shared) rather than WaveSpawnedTagComponent, which is server-only.
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
