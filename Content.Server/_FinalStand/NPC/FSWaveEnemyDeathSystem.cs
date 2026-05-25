using Content.Server._FinalStand.Spawners;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._FinalStand.NPC;

/// <summary>
/// Clears collision layer and mask on wave enemy corpses so bullets pass through them.
/// Without this, dead zombies retain MobLayer (which includes BulletImpassable) and eat
/// projectiles — StandingStateSystem.Down() only removes MidImpassable from the mask.
/// FS zombies have MovementIgnoreGravity so zeroing fixtures is safe.
/// </summary>
public sealed class FSWaveEnemyDeathSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveSpawnedTagComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(EntityUid uid, WaveSpawnedTagComponent _, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        foreach (var (key, fixture) in fixtures.Fixtures)
        {
            _physics.SetCollisionLayer(uid, key, fixture, 0, fixtures);
            _physics.SetCollisionMask(uid, key, fixture, 0, fixtures);
        }
    }
}
