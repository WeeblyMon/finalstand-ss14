using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.Explosion;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ChemistTier0Test : GameTest
{
    private const string HumanProto = "MobHuman";
    private const string MedipenProto = "EmergencyMedipen";

    [Test]
    public async Task ReagentExplosionsSpareCrew()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var crew = entMan.SpawnEntity(HumanProto, map.GridCoords);
            entMan.EnsureComponent<FSFriendlyFireComponent>(crew);

            foreach (var proto in new[] { "Default", "FSGrenadeExplosion", "FSLandmineExplosion", "FSMartyrExplosion" })
            {
                var ev = new GetExplosionResistanceEvent(proto);
                entMan.EventBus.RaiseLocalEvent(crew, ref ev);

                Assert.That(ev.DamageCoefficient, Is.EqualTo(0f),
                    $"'{proto}' still hurts crew - the filter is an allowlist again");
            }
        });
    }

    [Test]
    public async Task EnemyExplosionsStillHurtCrew()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var crew = entMan.SpawnEntity(HumanProto, map.GridCoords);
            entMan.EnsureComponent<FSFriendlyFireComponent>(crew);

            var ev = new GetExplosionResistanceEvent("FSGiantStompExplosion");
            entMan.EventBus.RaiseLocalEvent(crew, ref ev);

            Assert.That(ev.DamageCoefficient, Is.EqualTo(1f),
                "enemy explosions must stay lethal - a blanket denylist would neuter boss attacks");
        });
    }

    [Test]
    public async Task AMedipenEmptiesInOneUse()
    {
        var server = Pair.Server;
        var protos = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var medipen = protos.Index<EntityPrototype>(MedipenProto);

            Assert.That(medipen.TryGetComponent<Content.Shared._Shitmed.Chemistry.HyposprayComponent>(
                    "Hypospray", out var hypo), Is.True,
                "medipens route through HypospraySystem, not the injector, so this is the component that matters");

            Assert.That(hypo!.TransferAmount.Int(), Is.GreaterThanOrEqualTo(30),
                "must cover the largest medipen's contents or it takes multiple clicks");
        });
    }
}
