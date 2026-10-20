using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Crit;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

// A bullet whose shooter or weapon was deleted mid-flight loses their effects, not the target's: an armoured
// zombie's deflect still stops it, and nothing reads the deleted weapon.
[TestFixture]
[TestOf(typeof(CritSystem))]
public sealed class OrphanedBulletTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> Piercing = "Piercing";

    [Test]
    public async Task DeflectStillStopsABulletWithADeletedShooter()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var zombie = entMan.SpawnEntity("FSZombieArmoured", new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            var shooter = entMan.SpawnEntity("FSZombieNormal", new EntityCoordinates(map.Grid, new Vector2(3.5f, 0.5f)));
            var weapon = entMan.SpawnEntity(null, new EntityCoordinates(map.Grid, new Vector2(3.5f, 0.5f)));
            var bullet = entMan.SpawnEntity(null, new EntityCoordinates(map.Grid, new Vector2(1.5f, 0.5f)));

#pragma warning disable RA0002
            var deflect = entMan.GetComponent<FSArmouredDeflectComponent>(zombie);
            deflect.IsGlowing = true;
            deflect.PhaseTimer = 1000f;

            var projectile = entMan.AddComponent<ProjectileComponent>(bullet);
            projectile.Shooter = shooter;
            projectile.Weapon = weapon;
#pragma warning restore RA0002

            entMan.DeleteEntity(shooter);

            var hit = new ProjectileHitEvent(new DamageSpecifier { DamageDict = { [Piercing] = 20 } }, zombie, shooter);
            entMan.EventBus.RaiseLocalEvent(bullet, ref hit);

            Assert.That(hit.Damage.GetTotal().Float(), Is.Zero, "the deflect didn't stop a bullet whose shooter was deleted");

            // Gone before it's networked with a dangling shooter.
            entMan.DeleteEntity(bullet);
        });
    }

    // A giant's boulder is its own weapon, and the giant is deleted when the CCC loses a life mid-throw.
    [Test]
    public async Task BulletWhoseWeaponWasItsDeletedShooterHitsAWall()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var classifier = entMan.System<FSWeaponClassifierSystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var wall = entMan.SpawnEntity("WallSolid", new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            var giant = entMan.SpawnEntity("FSZombieNormal", new EntityCoordinates(map.Grid, new Vector2(3.5f, 0.5f)));
            var boulder = entMan.SpawnEntity(null, new EntityCoordinates(map.Grid, new Vector2(1.5f, 0.5f)));

#pragma warning disable RA0002
            var projectile = entMan.AddComponent<ProjectileComponent>(boulder);
            projectile.Shooter = giant;
            projectile.Weapon = giant;
#pragma warning restore RA0002

            entMan.DeleteEntity(giant);

            Assert.Multiple(() =>
            {
                Assert.That(classifier.Classify(giant).HasGunTag, Is.False);
                Assert.That(classifier.IsSemiAutoRifle(giant), Is.False);
            });

            var hit = new ProjectileHitEvent(new DamageSpecifier { DamageDict = { [Piercing] = 20 } }, wall, giant);
            Assert.DoesNotThrow(() => entMan.EventBus.RaiseLocalEvent(boulder, ref hit));
            Assert.That(hit.Damage.GetTotal().Float(), Is.EqualTo(20f));

            entMan.DeleteEntity(boulder);
        });
    }
}
