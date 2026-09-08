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
