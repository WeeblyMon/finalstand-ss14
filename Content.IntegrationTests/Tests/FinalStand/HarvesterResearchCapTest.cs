using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Shared.Mind;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class HarvesterResearchCapTest : GameTest
{
    [TestCase("FSZombieNormal", 150)]
    [TestCase("FSZombieArmoured", 250)]
    public async Task HarvesterResearchStopsAtTheZombieCap(string zombieProto, int expected)
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var minds = entMan.System<SharedMindSystem>();
            var shooter = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, shooter);

            var gun = entMan.SpawnEntity("WeaponHarvesterFS", map.GridCoords);
            var zombie = entMan.SpawnEntity(zombieProto, map.GridCoords);

            for (var i = 0; i < 100; i++)
            {
                var ev = new HitscanRaycastFiredEvent
                {
                    Data = new HitscanRaycastFiredData
                    {
                        ShotDirection = Vector2.UnitX,
                        Gun = gun,
                        Shooter = shooter,
                        HitEntity = zombie,
                    },
                };
                entMan.EventBus.RaiseLocalEvent(gun, ref ev);
            }

            var cap = entMan.GetComponent<FSMoneyOnHitCapComponent>(zombie);
            cap.ResearchGivenPerPlayer.TryGetValue(mind, out var given);
            Assert.That(given, Is.EqualTo(expected));
        });
    }
}
