using Content.Server._FinalStand.Spawners;
using Content.Server.Projectiles;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;

namespace Content.Server._FinalStand.Upgrades;

public sealed class FSPierceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSPierceComponent, StartCollideEvent>(OnCollide, after: [typeof(ProjectileSystem)]);
    }

    private void OnCollide(EntityUid uid, FSPierceComponent pierce, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != SharedProjectileSystem.ProjectileFixture)
            return;
        if (!args.OtherFixture.Hard)
            return;
        if (!TryComp<ProjectileComponent>(uid, out var proj))
            return;
        if (!proj.ProjectileSpent)
            return;

        var target = args.OtherEntity;

        if (!HasComp<WaveSpawnedTagComponent>(target) || pierce.RemainingPierces <= 0)
        {
            QueueDel(uid);
            return;
        }

        pierce.RemainingPierces--;
        proj.ProjectileSpent = false;
    }
}
