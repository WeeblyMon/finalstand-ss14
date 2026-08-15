// Research bonuses key off these flags, so a wrong one silently changes weapon stats.

using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Weapons;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class WeaponClassifierTest : GameTest
{
    [Test]
    public async Task EachOrdnanceWeaponClassifiesAsItself()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var classifier = entityManager.System<FSWeaponClassifierSystem>();

            FSWeaponKind Classify(string proto) =>
                classifier.Classify(entityManager.SpawnEntity(proto, mapData.GridCoords));

            var l6 = Classify(FSWeaponClassifierSystem.L6Proto);
            var hydra = Classify(FSWeaponClassifierSystem.HydraProto);
            var rpg = Classify(FSWeaponClassifierSystem.RpgProto);
            var xray = Classify(FSWeaponClassifierSystem.XrayProto);
            var tesla = Classify(FSWeaponClassifierSystem.TeslaProto);
            var harvester = Classify(FSWeaponClassifierSystem.HarvesterProto);

            Assert.Multiple(() =>
            {
                Assert.That(l6.L6, Is.True, "L6 did not classify as the L6");
                Assert.That(l6.Ballistic, Is.True, "L6 lost its ballistic tag");

                Assert.That(hydra.Hydra, Is.True, "Hydra did not classify as the Hydra");
                Assert.That(hydra.Launcher, Is.True, "Hydra lost its launcher tag");

                Assert.That(rpg.Rpg, Is.True, "RPG did not classify as the RPG");
                Assert.That(rpg.Launcher, Is.True, "RPG lost its launcher tag");

                Assert.That(xray.Xray, Is.True, "X-Ray did not classify as the X-Ray");
                Assert.That(tesla.Tesla, Is.True, "Tesla did not classify as the Tesla");

                // The Harvester carries none of the three gun tags, which is why the systems that
                // gate on HasGunTag have to test it separately.
                Assert.That(harvester.Harvester, Is.True, "Harvester did not classify as the Harvester");
                Assert.That(harvester.HasGunTag, Is.False,
                    "Harvester now carries a gun tag - the callers that special-case it need revisiting");
            });

            Assert.Multiple(() =>
            {
                Assert.That(l6.Hydra || l6.Rpg || l6.Xray || l6.Tesla || l6.Harvester, Is.False,
                    "L6 also classified as another weapon");
                Assert.That(xray.Tesla || xray.L6 || xray.Hydra, Is.False,
                    "X-Ray also classified as another weapon");
            });
        });
    }
}
