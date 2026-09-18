using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class PotionInSyringeTest : GameTest
{
    [Test]
    public async Task EveryThrownBuffAlsoWorksWhenInjected()
    {
        var server = Pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var checkedAny = false;

            foreach (var reagent in protoMan.EnumeratePrototypes<ReagentPrototype>())
            {
                var thrown = ReactiveBuffs(reagent).ToArray();
                if (thrown.Length == 0)
                    continue;

                checkedAny = true;

                var injected = MetabolismBuffs(reagent).Select(b => b.Source).ToHashSet();

                foreach (var buff in thrown)
                {
                    Assert.That(injected, Does.Contain(buff.Source),
                        $"Reagent '{reagent.ID}' applies combat buff '{buff.Source}' on Touch but has no " +
                        "matching metabolism effect, so loading it into a syringe magazine and firing it " +
                        "at a crewmate would deliver the reagent and apply nothing.");
                }
            }

            Assert.That(checkedAny, Is.True, "No thrown combat buffs found - this guard is not testing anything.");
        });
    }

    private static IEnumerable<FSApplyCombatBuff> ReactiveBuffs(ReagentPrototype reagent)
    {
        if (reagent.ReactiveEffects is not { } reactives)
            yield break;

        foreach (var (_, reactive) in reactives)
        {
            foreach (var effect in reactive.Effects)
            {
                if (effect is FSApplyCombatBuff buff)
                    yield return buff;
            }
        }
    }

    private static IEnumerable<FSApplyCombatBuff> MetabolismBuffs(ReagentPrototype reagent)
    {
        if (reagent.Metabolisms?.Metabolisms is not { } metabolisms)
            yield break;

        foreach (var (_, entry) in metabolisms)
        {
            foreach (var effect in entry.Effects)
            {
                if (effect is FSApplyCombatBuff buff)
                    yield return buff;
            }
        }
    }
}
