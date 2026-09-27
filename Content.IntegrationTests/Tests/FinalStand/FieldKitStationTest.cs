#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class FieldKitStationTest : GameTest
{
    private const string StationProto = "FSFieldKitStation";
    private const string BeakerProto = "LargeBeaker";
    private const string SatchelProto = "FSHarvestSatchel";
    private const string FlaskProto = "FSSplashFlask";
    private const string MagazineProto = "FSSyringePack";
    private const string HumanProto = "MobHuman";
    private const string Tissue = "FSBiomass";
    private const string Stim = "FSCombatStim";
    private const string Sugar = "Sugar";

    [Test]
    public async Task BrewsPackagesAndReturnsTissue()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();
        EntityUid station = default;

        await server.WaitPost(() =>
        {
            station = entMan.SpawnEntity(StationProto, map.GridCoords);
            entMan.System<SharedPowerReceiverSystem>().SetNeedsPower(station, false);
        });
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var solutions = entMan.System<SharedSolutionContainerSystem>();
            var slots = entMan.System<ItemSlotsSystem>();
            var containers = entMan.System<SharedContainerSystem>();

            var chemist = entMan.SpawnEntity(HumanProto, map.GridCoords);

            var satchel = entMan.SpawnEntity(SatchelProto, map.GridCoords);
            Assert.That(solutions.TryGetSolution(satchel, "satchel", out var satchelSoln, out _));
            solutions.TryAddReagent(satchelSoln!.Value, Tissue, 120);
            Assert.That(entMan.System<SharedHandsSystem>().TryPickupAnyHand(chemist, satchel));

            Raise(entMan, station, chemist, new FSFieldKitDumpSatchelMessage());
            Assert.That(solutions.TryGetSolution(station, "tank", out var tankSoln, out var tank));
            Assert.That(tank!.GetTotalPrototypeQuantity(Tissue), Is.EqualTo(FixedPoint2.New(120)), "the dump should move the whole satchel into the tank");

            var beaker = entMan.SpawnEntity(BeakerProto, map.GridCoords);
            Assert.That(slots.TryInsert(station, "jug", beaker, null));
            Assert.That(solutions.TryGetFitsInDispenser(beaker, out _, out var jug));
            var sugarBefore = StockOf(entMan, solutions, station, Sugar);

            Raise(entMan, station, chemist, new FSFieldKitBrewMessage("FSFieldKitCombatStim", 100));
            Assert.Multiple(() =>
            {
                Assert.That(jug!.GetTotalPrototypeQuantity(Stim), Is.EqualTo(FixedPoint2.New(100)), "a 100u batch should land as 100u of product");
                Assert.That(jug.GetTotalPrototypeQuantity(Tissue), Is.EqualTo(FixedPoint2.Zero), "no raw tissue should reach the jug");
                Assert.That(tank.GetTotalPrototypeQuantity(Tissue), Is.EqualTo(FixedPoint2.New(20)), "100u of stim costs 100u of tissue");
                Assert.That(StockOf(entMan, solutions, station, Sugar), Is.EqualTo(sugarBefore - 50), "100u of stim costs 50u of sugar");
            });

            var flask = entMan.SpawnEntity(FlaskProto, map.GridCoords);
            containers.Insert(flask, containers.EnsureContainer<Container>(station, "flaskRack"));
            Raise(entMan, station, chemist, new FSFieldKitFillFlasksMessage(50, 1));
            Assert.That(solutions.TryGetSolution(flask, "flask", out _, out var flaskContents));
            Assert.Multiple(() =>
            {
                Assert.That(flaskContents!.GetTotalPrototypeQuantity(Stim), Is.EqualTo(FixedPoint2.New(50)));
                Assert.That(containers.IsEntityInContainer(flask), Is.False, "a filled flask leaves the rack");
                Assert.That(jug!.Volume, Is.EqualTo(FixedPoint2.New(50)));
            });

            var magazine = entMan.SpawnEntity(MagazineProto, map.GridCoords);
            Assert.That(slots.TryInsert(station, "magazine", magazine, null));
            Raise(entMan, station, chemist, new FSFieldKitLoadMagazineMessage());
            Assert.That(solutions.TryGetSolution(magazine, "pack", out _, out var pack));
            Assert.Multiple(() =>
            {
                Assert.That(pack!.GetTotalPrototypeQuantity(Stim), Is.EqualTo(FixedPoint2.New(50)));
                Assert.That(jug.Volume, Is.EqualTo(FixedPoint2.Zero));
            });

            Raise(entMan, station, chemist, new FSFieldKitBrewMessage("FSFieldKitCombatStim", 100));
            Assert.That(jug.Volume, Is.EqualTo(FixedPoint2.Zero), "a batch the tank cannot cover must not brew at all");

            Raise(entMan, station, chemist, new FSFieldKitDispenseMessage(null, 15));
            Assert.That(tank.GetTotalPrototypeQuantity(Tissue), Is.EqualTo(FixedPoint2.New(5)));
            Raise(entMan, station, chemist, new FSFieldKitDiscardMessage(Tissue));
            Assert.That(tank.GetTotalPrototypeQuantity(Tissue), Is.EqualTo(FixedPoint2.New(20)), "discarded tissue goes back to the tank");
        });
    }

    private static void Raise<T>(IEntityManager entMan, EntityUid station, EntityUid actor, T message) where T : BoundUserInterfaceMessage
    {
        message.Actor = actor;
        entMan.EventBus.RaiseLocalEvent(station, message);
    }

    private static FixedPoint2 StockOf(IEntityManager entMan, SharedSolutionContainerSystem solutions, EntityUid station, string reagent)
    {
        var total = FixedPoint2.Zero;
        foreach (var stored in entMan.GetComponent<Content.Shared.Storage.StorageComponent>(station).StoredItems.Keys)
        {
            if (solutions.TryGetDrainableSolution(stored, out _, out var solution))
                total += solution.GetTotalPrototypeQuantity(reagent);
        }
        return total;
    }
}
