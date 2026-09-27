using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Perks;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared._Shitmed.Body.Organ;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class UndyingTest : GameTest
{
    private const string HumanProto = "MobHuman";

    [TestCase(120)]
    [TestCase(300)]
    [TestCase(600)]
    public async Task UndyingCatchesTheBlowThatDownsYou(int damage)
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            human = entMan.SpawnEntity(HumanProto, map.GridCoords);
            entMan.EnsureComponent<FSUndyingComponent>(human).Level = 4;
            entMan.System<DamageableSystem>().TryChangeDamage(human,
                new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), damage),
                ignoreResistances: true);
        });
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<MobStateComponent>(human).CurrentState, Is.EqualTo(MobState.Alive),
                $"{damage} damage in one hit should leave an Undying player standing");
            Assert.That(entMan.System<FSUndyingSystem>().IsActive(human), Is.True);
            Assert.That(entMan.HasComponent<DebrainedComponent>(human), Is.False, "the hit should not have destroyed the brain");
            Assert.That(entMan.HasComponent<StunnedComponent>(human), Is.False, "an Undying player must still be able to act");
        });
    }
}
