using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Perks;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ArchetypeTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task ABulwarkKeystoneTakesLessDamage()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var minds = SEntMan.System<SharedMindSystem>();
            var damageable = SEntMan.System<DamageableSystem>();
            var session = ServerSession!;

            var body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var mindId = minds.CreateMind(session.UserId, "Bulwark");
            minds.TransferTo(mindId, body, mind: SEntMan.GetComponent<MindComponent>(mindId));
            var perks = SEntMan.EnsureComponent<FSPerkLevelsComponent>(mindId);

            var hit = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(20) } };
            FixedPoint2 Hit()
            {
                damageable.SetAllDamage(body, 0);
                damageable.TryChangeDamage(body, hit, ignoreResistances: false);
                return SEntMan.GetComponent<DamageableComponent>(body).TotalDamage;
            }

            var plain = Hit();
            Assert.That(plain, Is.GreaterThan(FixedPoint2.Zero), "test precondition: the hit lands");

            var blues = new[] { "LifeLeech", "FieldMedic", "LegBreaker", "KnockbackBlast", "CombatMedic" };
            Assert.That(blues.All(id => FSPerkDef.All[id].Category == PerkCategory.Blue), "test precondition");
            for (var i = 0; i < blues.Length; i++)
            {
                perks.Levels[blues[i]] = 1;
                perks.Slots[i] = blues[i];
            }
            perks.Invalidate();

            Assert.That(SEntMan.System<FSArchetypeSystem>().Get(mindId),
                Is.EqualTo(new FSArchetype(PerkCategory.Blue, FSArchetypeTier.Keystone, 5)));
            var guarded = Hit();
            Assert.That(guarded.Float(), Is.EqualTo(plain.Float() * 0.8f).Within(0.05f),
                "a Bulwark keystone shrugs off a fifth of every hit");
        });
    }
}
