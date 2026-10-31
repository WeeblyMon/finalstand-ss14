using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Weapons;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

// Radiation Coating's DoT ticks in steps. A hit on a target already burning refreshes the DoT but keeps the step
// that's due; restarting the step timer on a hit halved the DoT of an X-ray firing in time with it.
[TestFixture]
[TestOf(typeof(FSRadiationMarkSystem))]
public sealed class RadiationDotStepTest : GameTest
{
    private const string Zombie = "FSZombieNormal";
    private const string Human = "MobHuman";
    private const string Xray = "FSXrayLaser";

    [Test]
    public async Task HitOnTheStepTickKeepsTheStep()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var shooter = entMan.SpawnEntity(Human, map.GridCoords);
            var gun = entMan.SpawnEntity(null, map.GridCoords);
#pragma warning disable RA0002
            entMan.AddComponent<FSWeaponUpgradeStateComponent>(gun).RadiationCoatingLevel = 1;
#pragma warning restore RA0002
            var zombie = entMan.SpawnEntity(Zombie, map.GridCoords);

            Hit(entMan, map.GridCoords, gun, shooter, zombie);
            var mark = entMan.GetComponent<FSRadiationMarkComponent>(zombie);
            Assert.That(mark.HasDot, "the coated hit started no DoT");

            // The next step falls due on the same tick as the next hit.
            var due = timing.CurTime;
            mark.NextDot = due;
            Hit(entMan, map.GridCoords, gun, shooter, zombie);
            Assert.That(mark.NextDot, Is.EqualTo(due), "a hit pushed back a DoT step that was due");
        });
    }

    private static void Hit(IEntityManager entMan, Robust.Shared.Map.EntityCoordinates coords, EntityUid gun,
        EntityUid shooter, EntityUid target)
    {
        var beam = entMan.SpawnEntity(Xray, coords);
        var ev = new HitscanRaycastFiredEvent
        {
            Data = new HitscanRaycastFiredData { Gun = gun, Shooter = shooter, HitEntity = target },
        };
        entMan.EventBus.RaiseLocalEvent(beam, ref ev);
        entMan.DeleteEntity(beam);
    }
}
