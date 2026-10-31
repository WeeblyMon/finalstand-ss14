using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Emplacement;
using Content.Server._FinalStand.Fortifications;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Fortifications;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

// The emplacement packs up at the end of every wave; upgrades poured into its hardpoints have to come back with it.
[TestFixture]
public sealed class EmplacementPackTest : GameTest
{
    private const string CoreProto = "FSEmplacementCore";
    private const string SentryProto = "FSEmplacementSentry";

    private static readonly ProtoId<FSUpgradePrototype> Servos = "FSModuleServos";
    private static readonly ProtoId<FSUpgradePrototype> Optics = "FSModuleOptics";
    private static readonly ProtoId<FSUpgradePrototype> Belt = "FSModuleBelt";

    [Test]
    public async Task HardpointUpgradesSurvivePacking()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var emplacement = entMan.System<FSEmplacementSystem>();
            var upgrades = entMan.System<FSUpgradeSystem>();

            var core = entMan.SpawnEntity(CoreProto, map.GridCoords);
            var sentry = entMan.SpawnEntity(SentryProto, map.GridCoords);
            var coreComp = entMan.GetComponent<FSEmplacementComponent>(core);
#pragma warning disable RA0002
            coreComp.Deployed = true;
            coreComp.Pieces.Add(sentry);
#pragma warning restore RA0002

            var upgradeable = entMan.GetComponent<FSUpgradeableComponent>(sentry);
            upgrades.Apply((sentry, upgradeable), protos.Index(Servos));
            upgrades.Apply((sentry, upgradeable), protos.Index(Optics));
            upgradeable.Pending = Belt;
            upgradeable.Poured = 1;

            var upgraded = entMan.GetComponent<FSSentryTurretComponent>(sentry);
            var fireInterval = upgraded.FireInterval;
            var range = upgraded.Range;

            var kit = emplacement.Pack((core, coreComp));
            Assert.That(entMan.TryGetComponent<FSPackedEmplacementComponent>(kit, out var packed));
            Assert.That(packed!.Pieces, Has.Count.EqualTo(1));
            Assert.That(packed.Pieces[0], Is.Not.Null);

            var rebuilt = entMan.SpawnEntity(SentryProto, map.GridCoords);
            var rebuiltUpgrades = entMan.GetComponent<FSUpgradeableComponent>(rebuilt);
            upgrades.Restore((rebuilt, rebuiltUpgrades), packed.Pieces[0]!.Value);

            var turret = entMan.GetComponent<FSSentryTurretComponent>(rebuilt);
            Assert.Multiple(() =>
            {
                Assert.That(rebuiltUpgrades.Modules, Is.EquivalentTo(new[] { Servos, Optics }));
                Assert.That(rebuiltUpgrades.Pending, Is.EqualTo(Belt), "a half-poured upgrade was paid for per hit");
                Assert.That(rebuiltUpgrades.Poured, Is.EqualTo(1));
                Assert.That(turret.FireInterval, Is.EqualTo(fireInterval).Within(1e-5f));
                Assert.That(turret.Range, Is.EqualTo(range).Within(1e-5f));
            });
        });
    }
}
