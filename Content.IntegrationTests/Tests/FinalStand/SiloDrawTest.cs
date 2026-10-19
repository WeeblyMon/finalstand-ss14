using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Materials;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

// Steel sheets and rods both draw on the silo's steel. Each fits on its own here, together they don't,
// and checking them separately used to let the build through with only part of it paid for.
[TestFixture]
public sealed class SiloDrawTest : GameTest
{
    private const string SiloProto = "MachineMaterialSilo";
    private const string HumanProto = "MobHuman";
    private const string SteelMaterial = "Steel";

    private static readonly ProtoId<StackPrototype> SteelSheet = "Steel";
    private static readonly ProtoId<StackPrototype> Rod = "MetalRod";

    [Test]
    public async Task StacksSharingAMaterialAreCountedTogether()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var silo = entMan.System<FSSiloNetworkSystem>();
            var storage = entMan.System<SharedMaterialStorageSystem>();

            var pool = entMan.SpawnEntity(SiloProto, map.GridCoords);
            var user = entMan.SpawnEntity(HumanProto, map.GridCoords);

            Assert.That(silo.SheetMaterial(SteelSheet)?.Material, Is.EqualTo(SteelMaterial));
            Assert.That(silo.SheetMaterial(Rod)?.Material, Is.EqualTo(SteelMaterial));

            var sheetVolume = silo.SheetMaterial(SteelSheet)!.Value.PerSheet;
            Assert.That(storage.TryChangeMaterialAmount(pool, SteelMaterial, sheetVolume * 2), Is.True);
            Assert.That(silo.PoolFor(user), Is.EqualTo(pool));

            var sheetsOnly = new Dictionary<ProtoId<StackPrototype>, int> { [SteelSheet] = 2 };
            var rodsOnly = new Dictionary<ProtoId<StackPrototype>, int> { [Rod] = 4 };
            var both = new Dictionary<ProtoId<StackPrototype>, int> { [SteelSheet] = 2, [Rod] = 4 };

            Assert.That(silo.Has(user, sheetsOnly), Is.True);
            Assert.That(silo.Has(user, rodsOnly), Is.True);
            Assert.That(silo.Has(user, both), Is.False);
            Assert.That(silo.TryConsume(user, both), Is.False);
            Assert.That(storage.GetMaterialAmount(pool, SteelMaterial), Is.EqualTo(sheetVolume * 2),
                "a refused draw must not take anything");

            Assert.That(silo.TryConsume(user, sheetsOnly), Is.True);
            Assert.That(storage.GetMaterialAmount(pool, SteelMaterial), Is.Zero);
        });
    }
}
