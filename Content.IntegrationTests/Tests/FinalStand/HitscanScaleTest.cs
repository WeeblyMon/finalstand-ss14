using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.Crit;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// Hitscan damage is scaled before it lands, like a projectile's. Paying only the surplus after vanilla had dealt the
// base threw away anything below 1: a glowing armoured zombie took full damage from a laser and still deflected it.
[TestFixture]
[TestOf(typeof(FSHitscanEffectSystem))]
public sealed class HitscanScaleTest : GameTest
{
    private const string Armoured = "FSZombieArmoured";
    private const string Human = "MobHuman";
    private const string Rifle = "FSWeaponPulseRifle";
    private const string Pulse = "FSPulse";

    [Test]
    public async Task GlowingArmourTakesNoHitscanDamage()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var shooter = entMan.SpawnEntity(Human, map.GridCoords);
            var rifle = entMan.SpawnEntity(Rifle, map.GridCoords);
            var zombie = entMan.SpawnEntity(Armoured, map.GridCoords);
            var deflect = entMan.GetComponent<FSArmouredDeflectComponent>(zombie);
            deflect.IsGlowing = true;
            deflect.PhaseTimer = 1000f;
            // Its armour layer would soak the hit before health either way.
            entMan.RemoveComponent<FSArmorComponent>(zombie);

            Fire(entMan, map.GridCoords, rifle, shooter, zombie);
            Assert.That(entMan.GetComponent<DamageableComponent>(zombie).TotalDamage, Is.EqualTo(FixedPoint2.Zero),
                "a deflected laser still hurt the armoured zombie");
        });
    }

    [Test]
    public async Task DamageUpgradeLandsOnce()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var shooter = entMan.SpawnEntity(Human, map.GridCoords);
            var rifle = entMan.SpawnEntity(Rifle, map.GridCoords);
            var plain = entMan.SpawnEntity(Human, map.GridCoords);
            var doubled = entMan.SpawnEntity(Human, map.GridCoords);
            // The rifle can crit; keep the comparison to the upgrade alone.
            entMan.EnsureComponent<CritImmuneComponent>(plain);
            entMan.EnsureComponent<CritImmuneComponent>(doubled);

            Fire(entMan, map.GridCoords, rifle, shooter, plain);
            entMan.EnsureComponent<FSWeaponUpgradeStateComponent>(rifle).DamageMultiplier = 2f;
            Fire(entMan, map.GridCoords, rifle, shooter, doubled);

            var basis = entMan.GetComponent<DamageableComponent>(plain).TotalDamage;
            Assert.That(basis, Is.GreaterThan(FixedPoint2.Zero), "the pulse did no damage");
            Assert.That(entMan.GetComponent<DamageableComponent>(doubled).TotalDamage, Is.EqualTo(basis * 2),
                "a 2x damage upgrade didn't deal twice the damage");
        });
    }

    private static void Fire(IEntityManager entMan, Robust.Shared.Map.EntityCoordinates coords, EntityUid gun,
        EntityUid shooter, EntityUid target)
    {
        var pulse = entMan.SpawnEntity(Pulse, coords);
        var ev = new HitscanRaycastFiredEvent
        {
            Data = new HitscanRaycastFiredData
            {
                Gun = gun,
                Shooter = shooter,
                HitEntity = target,
            },
        };
        entMan.EventBus.RaiseLocalEvent(pulse, ref ev);
        entMan.DeleteEntity(pulse);
    }
}
