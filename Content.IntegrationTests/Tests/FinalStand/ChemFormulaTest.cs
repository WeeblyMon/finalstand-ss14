using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.MedicalOps;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ChemFormulaTest : GameTest
{
    private const string LauncherProto = "FSChemLauncher";

    [Test]
    public async Task EveryFormulaResolves()
    {
        var server = Pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            var formulas = protoMan.EnumeratePrototypes<FSChemFormulaPrototype>().ToArray();
            Assert.That(formulas, Is.Not.Empty, "No chem formulas are defined.");

            foreach (var formula in formulas)
            {
                Assert.That(formula.Cost, Is.GreaterThan(0), $"Formula '{formula.ID}' is free to fire.");

                Assert.That(protoMan.TryIndex(formula.Reagent, out var reagent), Is.True,
                    $"Formula '{formula.ID}' names reagent '{formula.Reagent}', which does not exist.");
                Assert.That(reagent!.ReactiveEffects?.Values.Any(r => r.Methods.Contains(ReactionMethod.Touch)), Is.True,
                    $"Formula '{formula.ID}' fires '{formula.Reagent}', which has no Touch effect, so its cloud does nothing.");

                Assert.That(protoMan.TryIndex(formula.Canister, out var canister), Is.True,
                    $"Formula '{formula.ID}' names canister '{formula.Canister}', which does not exist.");
                Assert.That(canister!.TryGetComponent<FSChemCanisterComponent>(out var comp, factory), Is.True,
                    $"Canister '{formula.Canister}' has no FSChemCanister, so it lands without a cloud.");
                Assert.That(comp!.Formula.Id, Is.EqualTo(formula.ID),
                    $"Canister '{formula.Canister}' bursts as '{comp.Formula}', not '{formula.ID}'.");

                if (formula.Research is { } node)
                {
                    Assert.That(protoMan.HasIndex<FSTechNodePrototype>(node), Is.True,
                        $"Formula '{formula.ID}' is unlocked by '{node}', which is not a real node.");
                }
            }
        });
    }

    [Test]
    public async Task EveryFormulaHasResolvableText()
    {
        var server = Pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var locMan = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var formula in protoMan.EnumeratePrototypes<FSChemFormulaPrototype>())
            {
                foreach (var (label, id) in new[]
                         {
                             ("name", formula.Name.Id),
                             ("purpose", formula.Purpose.Id),
                         })
                {
                    Assert.That(locMan.HasString(id), Is.True,
                        $"Formula '{formula.ID}' has an unresolved {label} string '{id}'.");
                }
            }
        });
    }

    [Test]
    public async Task TheLauncherKnowsExactlyWhatResearchAllows()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var upgrades = entMan.System<FSMedicalUpgradeSystem>();

            var uid = entMan.SpawnEntity(LauncherProto, map.GridCoords);
            var launcher = entMan.GetComponent<FSChemLauncherComponent>(uid);

            Assert.Multiple(() =>
            {
                foreach (var formula in protoMan.EnumeratePrototypes<FSChemFormulaPrototype>())
                {
                    var allowed = formula.Research is not { } node || upgrades.Unlocked(node.Id);
                    Assert.That(launcher.Known.Contains(formula.ID), Is.EqualTo(allowed),
                        $"Formula '{formula.ID}' should {(allowed ? "" : "not ")}be on the launcher with the current research.");
                }

                foreach (var slot in launcher.Slots)
                {
                    Assert.That(launcher.Known, Does.Contain(slot),
                        $"The launcher starts loaded with '{slot}', which it does not know.");
                }

                Assert.That(launcher.Slots, Has.Count.LessThanOrEqualTo(launcher.SlotCount));
                Assert.That(launcher.Reserve, Is.EqualTo(launcher.MaxReserve), "a new launcher must start full");
            });
        });
    }
}
