using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class AllyProjectileTest : GameTest
{
    private const string HumanProto = "MobHuman";
    private const string BulletProto = "BulletRifle";

    [Test]
    public async Task OrdinaryProjectilesPassThroughTeammates()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var shooter = entMan.SpawnEntity(HumanProto, map.GridCoords);
            var ally = entMan.SpawnEntity(HumanProto, map.GridCoords);
            entMan.EnsureComponent<FSFriendlyFireComponent>(shooter);
            entMan.EnsureComponent<FSFriendlyFireComponent>(ally);

            var bullet = entMan.SpawnEntity(BulletProto, map.GridCoords);
            entMan.GetComponent<ProjectileComponent>(bullet).Shooter = shooter;

            Assert.That(Collides(entMan, ally, bullet), Is.False,
                "a normal bullet must keep passing through teammates");
        });
    }

    [Test]
    public async Task MarkedProjectilesReachTeammates()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var shooter = entMan.SpawnEntity(HumanProto, map.GridCoords);
            var ally = entMan.SpawnEntity(HumanProto, map.GridCoords);
            entMan.EnsureComponent<FSFriendlyFireComponent>(shooter);
            entMan.EnsureComponent<FSFriendlyFireComponent>(ally);

            var dart = entMan.SpawnEntity(BulletProto, map.GridCoords);
            entMan.GetComponent<ProjectileComponent>(dart).Shooter = shooter;
            entMan.EnsureComponent<FSAllyProjectileComponent>(dart);

            Assert.That(Collides(entMan, ally, dart), Is.True,
                "the chemist syringe must be able to embed in a teammate or it can never inject");
        });
    }

    [Test]
    public async Task MarkedProjectilesStillDoNotHurtTeammates()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var shooter = entMan.SpawnEntity(HumanProto, map.GridCoords);
            var ally = entMan.SpawnEntity(HumanProto, map.GridCoords);
            entMan.EnsureComponent<FSFriendlyFireComponent>(shooter);
            entMan.EnsureComponent<FSFriendlyFireComponent>(ally);

            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Piercing", 5);

            var ev = new BeforeDamageChangedEvent(damage, shooter);
            entMan.EventBus.RaiseLocalEvent(ally, ref ev);

            Assert.That(ev.Cancelled, Is.True,
                "allowing collision must not also let the syringe's Piercing damage land on the ally");
        });
    }

    [Test]
    public async Task TheChemistSyringeIsAMarkedGunRound()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<Robust.Shared.Prototypes.IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var syringe = entMan.SpawnEntity("FSChemistSyringe", map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<FSAllyProjectileComponent>(syringe), Is.True,
                    "without the marker it phases through the teammate it is aimed at");

                var tags = entMan.GetComponent<Content.Shared.Tag.TagComponent>(syringe);
                Assert.That(tags.Tags, Does.Contain("SyringeGunAmmo"),
                    "it must still load into the syringe gun");
            });

            var gun = protos.Index<Robust.Shared.Prototypes.EntityPrototype>("LauncherSyringe");
            Assert.That(gun.TryGetComponent<Content.Shared.Storage.StorageComponent>("Storage", out _), Is.True);
        });
    }

    [Test]
    public async Task TheChemistSpawnsWithTheKit()
    {
        var server = Pair.Server;
        var protos = server.ResolveDependency<Robust.Shared.Prototypes.IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var gear = protos.Index<Content.Shared.Roles.StartingGearPrototype>("ChemistGear");

            Assert.That(gear.Storage.TryGetValue("back", out var back), Is.True,
                "the chemist must not have to go find their core tool");
            Assert.That(back, Does.Contain("LauncherSyringe"));
            Assert.That(back, Does.Contain("FSChemistSyringe"));
        });
    }

    private static bool Collides(IEntityManager entMan, EntityUid target, EntityUid projectile)
    {
        var ourBody = entMan.GetComponent<PhysicsComponent>(target);
        var otherBody = entMan.GetComponent<PhysicsComponent>(projectile);
        var ourFixture = entMan.GetComponent<FixturesComponent>(target).Fixtures.Values.First();
        var otherFixture = entMan.GetComponent<FixturesComponent>(projectile).Fixtures.Values.First();

        var ev = new PreventCollideEvent(target, projectile, ourBody, otherBody, ourFixture, otherFixture);
        entMan.EventBus.RaiseLocalEvent(target, ref ev);
        return !ev.Cancelled;
    }
}
