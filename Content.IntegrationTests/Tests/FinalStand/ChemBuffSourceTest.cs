using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ChemBuffSourceTest : GameTest
{
    [Test]
    public async Task EveryCombatBuffSourceCarriesThePrefix()
    {
        var server = Pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var checkedAny = false;

            foreach (var reagent in protoMan.EnumeratePrototypes<ReagentPrototype>())
            {
                foreach (var effect in CombatBuffsOf(reagent))
                {
                    checkedAny = true;
                    Assert.That(effect.Source, Does.StartWith(FSApplyCombatBuff.SourcePrefix),
                        $"Reagent '{reagent.ID}' applies a combat buff with source '{effect.Source}', " +
                        $"which lacks the '{FSApplyCombatBuff.SourcePrefix}' prefix, so the buff indicator will never show.");
                }
            }

            Assert.That(checkedAny, Is.True, "No combat buff effects found - this guard is not testing anything.");
        });
    }

    private static IEnumerable<FSApplyCombatBuff> CombatBuffsOf(ReagentPrototype reagent)
    {
        if (reagent.Metabolisms?.Metabolisms is { } metabolisms)
        {
            foreach (var (_, entry) in metabolisms)
            {
                foreach (var effect in entry.Effects)
                {
                    if (effect is FSApplyCombatBuff buff)
                        yield return buff;
                }
            }
        }

        if (reagent.ReactiveEffects is { } reactives)
        {
            foreach (var (_, reactive) in reactives)
            {
                foreach (var effect in reactive.Effects)
                {
                    if (effect is FSApplyCombatBuff buff)
                        yield return buff;
                }
            }
        }
    }
}
