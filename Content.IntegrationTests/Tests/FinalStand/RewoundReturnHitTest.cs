using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Upgrades;
using Content.Server._FinalStand.Weapons.Prediction;
using Content.Shared._FinalStand.Armor;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.FinalStand;

// With lag compensation on, the sweep is what lands a player's bullets on mobs. It has to act like a physics contact:
// a piercing bolt that leaves a zombie and comes back through it (a ricochet off a wall) hits it again, which is what
// Resonant Cascade crits on. Remembering every target for the bolt's whole life meant that second hit never came.
[TestFixture]
[TestOf(typeof(FSLagCompensationSystem))]
public sealed class RewoundReturnHitTest : GameTest
{
    private const string Zombie = "FSZombieNormal";
    private const string Human = "MobHuman";
    private const string Bullet = "BulletPistol";

    [Test]
    public async Task ReturningBoltHitsAgain()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var physics = entMan.System<SharedPhysicsSystem>();
        var xforms = entMan.System<SharedTransformSystem>();
        var pierce = entMan.System<FSPierceSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid zombie = default;
        EntityUid bullet = default;
        await server.WaitPost(() =>
        {
            for (var x = -8; x <= 14; x++)
                mapSystem.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);

            zombie = entMan.SpawnEntity(Zombie, new EntityCoordinates(map.Grid, new Vector2(5.5f, 0.5f)));
            // Its armour layer would soak the hits before health shows them.
            entMan.RemoveComponent<FSArmorComponent>(zombie);
            entMan.System<Content.Server.NPC.HTN.HTNSystem>().SetHTNEnabled(
                (zombie, entMan.GetComponent<Content.Server.NPC.HTN.HTNComponent>(zombie)), false);

            var shooter = entMan.SpawnEntity(Human, new EntityCoordinates(map.Grid, new Vector2(-7.5f, 0.5f)));
            bullet = entMan.SpawnEntity(Bullet, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));

#pragma warning disable RA0002
            var projectile = entMan.GetComponent<ProjectileComponent>(bullet);
            projectile.Shooter = shooter;
            projectile.Weapon = shooter;
            entMan.AddComponent<FSRewoundProjectileComponent>(bullet).Shooter = shooter;
#pragma warning restore RA0002
            pierce.GrantPierces(bullet, 3);

            physics.SetLinearVelocity(bullet, new Vector2(10f, 0f), body: entMan.GetComponent<PhysicsComponent>(bullet));
        });

        var first = await RunUntilPast(entMan, xforms, bullet, 8f, forward: true);
        Assert.That(TotalDamage(entMan, zombie), Is.GreaterThan(FixedPoint2.Zero), "the bolt never hit the zombie");
        Assert.That(first, "the bolt never got past the zombie");
        var afterFirst = TotalDamage(entMan, zombie);

        // Bounce it straight back.
        await server.WaitPost(() =>
            physics.SetLinearVelocity(bullet, new Vector2(-10f, 0f), body: entMan.GetComponent<PhysicsComponent>(bullet)));

        var back = await RunUntilPast(entMan, xforms, bullet, 3f, forward: false);
        Assert.That(back, "the bolt never came back past the zombie");
        Assert.That(TotalDamage(entMan, zombie), Is.GreaterThan(afterFirst), "the returning bolt didn't hit the zombie again");
    }

    private async Task<bool> RunUntilPast(IEntityManager entMan, SharedTransformSystem xforms, EntityUid bullet, float x,
        bool forward)
    {
        for (var i = 0; i < 120; i++)
        {
            await Pair.Server.WaitRunTicks(1);
            var past = false;
            await Pair.Server.WaitPost(() =>
            {
                var pos = xforms.GetWorldPosition(bullet).X;
                past = forward ? pos > x : pos < x;
            });

            if (past)
                return true;
        }

        return false;
    }

    private static FixedPoint2 TotalDamage(IEntityManager entMan, EntityUid uid)
    {
        return entMan.GetComponent<DamageableComponent>(uid).TotalDamage;
    }
}
