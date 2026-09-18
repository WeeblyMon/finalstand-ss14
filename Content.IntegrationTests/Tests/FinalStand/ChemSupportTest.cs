using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ChemSupportTest : GameTest
{
    private const string HumanProto = "MobHuman";
    private const string BulletProto = "BulletRifle";
    private const string GunProto = "WeaponRifleLecter";

    [Test]
    public async Task EtchantMakesTheNextShotsPierceArmour()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var reactive = entMan.System<ReactiveSystem>();

            var shooter = entMan.SpawnEntity(HumanProto, map.GridCoords);
            entMan.EnsureComponent<FSFriendlyFireComponent>(shooter);

            reactive.DoEntityReaction(shooter, new Solution("FSEtchant", 20), ReactionMethod.Touch);

            Assert.That(entMan.HasComponent<FSArmorPiercingComponent>(shooter), Is.True,
                "etchant must mark the crewmate, not the chemist");

            var gun = entMan.SpawnEntity(GunProto, map.GridCoords);
            var bullet = entMan.SpawnEntity(BulletProto, map.GridCoords);
            entMan.GetComponent<ProjectileComponent>(bullet).Shooter = shooter;

            var ev = new AmmoShotEvent { FiredProjectiles = new List<EntityUid> { bullet } };
            entMan.EventBus.RaiseLocalEvent(gun, ev);

            Assert.That(entMan.TryGetComponent<FSProjectileFlagsComponent>(bullet, out var flags), Is.True,
                "a buffed crewmate's shots must carry the piercing flag");
            Assert.That(flags!.Flags.HasFlag(FinalStandDamageFlags.ArmorPenetrating), Is.True);
        });
    }

    [Test]
    public async Task UnbuffedShotsAreUnaffected()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var shooter = entMan.SpawnEntity(HumanProto, map.GridCoords);
            var gun = entMan.SpawnEntity(GunProto, map.GridCoords);
            var bullet = entMan.SpawnEntity(BulletProto, map.GridCoords);
            entMan.GetComponent<ProjectileComponent>(bullet).Shooter = shooter;

            var ev = new AmmoShotEvent { FiredProjectiles = new List<EntityUid> { bullet } };
            entMan.EventBus.RaiseLocalEvent(gun, ev);

            Assert.That(entMan.HasComponent<FSProjectileFlagsComponent>(bullet), Is.False,
                "ordinary shots must not silently gain armour penetration");
        });
    }

    [Test]
    public async Task PotionsRequireTheirResearchedPrecursor()
    {
        var server = Pair.Server;
        var protos = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var (reaction, precursor) in new[]
                         {
                             ("FSCoagulantMist", "FSRefinedEnzyme"),
                             ("FSEtchant", "FSCatalystSalt"),
                         })
                {
                    var proto = protos.Index<ReactionPrototype>(reaction);

                    Assert.That(proto.Reactants.ContainsKey(precursor), Is.True,
                        $"{reaction} must be gated behind its researched precursor");
                    Assert.That(proto.Reactants.ContainsKey("FSBiomass"), Is.True,
                        $"{reaction} must still be gated behind harvesting");
                }
            });
        });
    }
}
