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

    // Lets a projectile carry on through at least this many wave enemies; separate sources don't stack, the highest wins.
    public void GrantPierces(EntityUid projectile, int pierces, ProjectileComponent? proj = null)
    {
        if (pierces <= 0 || !Resolve(projectile, ref proj, false))
            return;

        proj.DeleteOnCollide = false;
        var pierce = EnsureComp<FSPierceComponent>(projectile);
        pierce.RemainingPierces = Math.Max(pierce.RemainingPierces, pierces);
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

        if (pierce.AlreadyHit.Contains(target))
        {
            proj.ProjectileSpent = false;
            return;
        }

        if (!HasComp<WaveSpawnedTagComponent>(target) || pierce.RemainingPierces <= 0)
        {
            QueueDel(uid);
            return;
        }

        pierce.AlreadyHit.Add(target);
        pierce.RemainingPierces--;

        // Runs after ProjectileSystem, so this target kept full damage and the next one is halved.
        proj.Damage *= pierce.DamageRetained;
        proj.ProjectileSpent = false;
    }
}
