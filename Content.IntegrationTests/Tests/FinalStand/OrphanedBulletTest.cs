using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Crit;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

// A bullet whose shooter was deleted mid-flight loses the shooter's effects, not the target's: an armoured
// zombie's deflect still stops it.
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
}
