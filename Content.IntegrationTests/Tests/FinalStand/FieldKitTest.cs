using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class FieldKitTest : GameTest
{
    [Test]
    public async Task EveryEntryResolvesToRealReagents()
    {
        var server = Pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var kits = protoMan.EnumeratePrototypes<FSFieldKitPrototype>().ToArray();
            Assert.That(kits, Is.Not.Empty, "No field kit entries are defined.");

            foreach (var kit in kits)
            {
                var ingredients = kit.ResolveIngredients(protoMan);

                Assert.That(ingredients, Is.Not.Empty, $"Field kit '{kit.ID}' resolved to no ingredients.");

                foreach (var (reagent, amount) in ingredients)
                {
                    Assert.That(protoMan.HasIndex<ReagentPrototype>(reagent), Is.True,
                        $"Field kit '{kit.ID}' names reagent '{reagent}', which does not exist.");
                    Assert.That(amount.Value, Is.GreaterThan(0),
                        $"Field kit '{kit.ID}' asks for a non-positive amount of '{reagent}'.");
                }
            }
        });
    }

    [Test]
    public async Task ReactionBackedEntriesMatchTheirReaction()
    {
        var server = Pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var kit in protoMan.EnumeratePrototypes<FSFieldKitPrototype>())
            {
                if (kit.Reaction is not { } reaction)
                    continue;

                Assert.That(protoMan.Resolve(reaction, out var reactionProto), Is.True,
                    $"Field kit '{kit.ID}' references reaction '{reaction}', which does not exist.");

                var ingredients = kit.ResolveIngredients(protoMan);

                Assert.That(ingredients, Has.Count.EqualTo(reactionProto!.Reactants.Count),
                    $"Field kit '{kit.ID}' does not mirror the reactant count of '{reaction}'.");

                foreach (var (reactantId, reactant) in reactionProto.Reactants)
                {
                    Assert.That(ingredients.TryGetValue(reactantId, out var amount), Is.True,
                        $"Field kit '{kit.ID}' is missing reactant '{reactantId}' from '{reaction}'.");
                    Assert.That(amount, Is.EqualTo(reactant.Amount),
                        $"Field kit '{kit.ID}' disagrees with '{reaction}' on how much '{reactantId}' is needed.");
                }
            }
        });
    }

    [Test]
    public async Task EveryEntryHasResolvableText()
    {
        var server = Pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var locMan = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var kit in protoMan.EnumeratePrototypes<FSFieldKitPrototype>())
            {
                foreach (var (label, id) in new[]
                         {
                             ("name", kit.Name.Id),
                             ("purpose", kit.Purpose.Id),
                             ("delivery", kit.Delivery.Id),
                         })
                {
                    Assert.That(locMan.HasString(id), Is.True,
                        $"Field kit '{kit.ID}' has an unresolved {label} string '{id}'.");
                }
            }
        });
    }
}
