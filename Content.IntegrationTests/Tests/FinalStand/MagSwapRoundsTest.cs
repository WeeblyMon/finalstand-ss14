using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Shop;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// A magazine resizes to the gun it goes into. Moving it between a gun with a mag bonus and one without must keep its
// rounds: refilling whenever a full one grew handed back the bonus on every swap, which was endless ammo.
[TestFixture]
[TestOf(typeof(FSPlayerUpgradesSystem))]
public sealed class MagSwapRoundsTest : GameTest
{
    private const string Pistol = "WeaponPistolMk58";
    private const string Magazine = "MagazinePistol";
    private const int Bonus = 5;

    [TestCase(1)]
    [TestCase(Bonus)]
    public async Task SwappingAMagKeepsItsRounds(int fired)
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var slots = entMan.System<ItemSlotsSystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var upgraded = entMan.SpawnEntity(Pistol, map.GridCoords);
            var plain = entMan.SpawnEntity(Pistol, map.GridCoords);
            foreach (var gun in new[] { upgraded, plain })
            {
                if (slots.TryEject(gun, SharedGunSystem.MagazineSlot, null, out var starting))
                    entMan.DeleteEntity(starting.Value);
            }

#pragma warning disable RA0002
            entMan.EnsureComponent<FSWeaponUpgradeStateComponent>(upgraded).MagazineSizeBonus = Bonus;
            entMan.EnsureComponent<FSWeaponUpgradeStateComponent>(plain);
#pragma warning restore RA0002

            var mag = entMan.SpawnEntity(Magazine, map.GridCoords);
            var ammo = entMan.GetComponent<BallisticAmmoProviderComponent>(mag);
            var baseCapacity = ammo.Capacity;
            int Rounds() => ammo.Entities.Count + ammo.UnspawnedCount;

            Assert.That(slots.TryInsert(upgraded, SharedGunSystem.MagazineSlot, mag, null));
            Assert.That(Rounds(), Is.EqualTo(baseCapacity + Bonus), "a full mag didn't fill to the bonus size");

#pragma warning disable RA0002
            ammo.UnspawnedCount -= fired;
#pragma warning restore RA0002
            var left = Rounds();

            Assert.That(slots.TryEject(upgraded, SharedGunSystem.MagazineSlot, null, out _));
            Assert.That(slots.TryInsert(plain, SharedGunSystem.MagazineSlot, mag, null));
            Assert.That(slots.TryEject(plain, SharedGunSystem.MagazineSlot, null, out _));
            Assert.That(slots.TryInsert(upgraded, SharedGunSystem.MagazineSlot, mag, null));

            Assert.That(Rounds(), Is.EqualTo(left), "swapping the mag through a gun without the bonus changed its rounds");
        });
    }
}
