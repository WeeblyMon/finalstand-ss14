using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class MedievalistTest : GameTest
{
    [Test]
    public async Task TheOathRefusesBruteArmourOnly()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            var medievalist = SEntMan.System<FSMedievalistSystem>();
            var body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var vest = SEntMan.SpawnEntity("ClothingOuterArmorBasic", map.GridCoords);
            var labCoat = SEntMan.SpawnEntity("ClothingOuterCoatLab", map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(medievalist.IsBruteArmour(vest), Is.True, "0.7 blunt/slash/piercing is armour");
                Assert.That(medievalist.IsBruteArmour(labCoat), Is.False, "caustic protection alone isn't");
            });

            Assert.That(inventory.TryEquip(body, vest, "outerClothing", silent: true), Is.True, "test precondition");
            SEntMan.EnsureComponent<FSMedievalistComponent>(body);
            medievalist.StripArmour(body);
            Assert.That(inventory.TryGetSlotEntity(body, "outerClothing", out _), Is.False, "taking the oath strips worn armour");

            Assert.Multiple(() =>
            {
                Assert.That(inventory.TryEquip(body, vest, "outerClothing", silent: true), Is.False, "the oath refuses armour");
                Assert.That(inventory.TryEquip(body, labCoat, "outerClothing", silent: true), Is.True, "a lab coat is fine");
            });
        });
    }
}
