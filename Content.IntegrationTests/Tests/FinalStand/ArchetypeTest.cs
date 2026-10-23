using System.Collections.Generic;
using System.Linq;
using Content.Client._FinalStand.Perks.UI;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Perks;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Robust.Client.UserInterface.Controls;
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

    [Test]
    public async Task TheShopQuotesWhatTheArchetypePays()
    {
        // Progress at 2, archetype at 3, keystone at 5 of one category.
        var expected = new Dictionary<PerkCategory, string[]>
        {
            [PerkCategory.Red] = ["OFFENSE 2/3 for an archetype",
                "Slayer: +8% damage to the horde · keystone at 5 (3/5)", "Slayer KEYSTONE: +20% damage to the horde"],
            [PerkCategory.Blue] = ["DEFENSE 2/3 for an archetype",
                "Bulwark: 8% less damage taken · keystone at 5 (3/5)", "Bulwark KEYSTONE: 20% less damage taken"],
            [PerkCategory.Green] = ["UTILITY 2/3 for an archetype",
                "Operator: +6% move speed · keystone at 5 (3/5)", "Operator KEYSTONE: +15% move speed"],
            [PerkCategory.Yellow] = ["ECONOMY 2/3 for an archetype",
                "Tycoon: +12% kill credits · keystone at 5 (3/5)", "Tycoon KEYSTONE: +30% kill credits"],
            [PerkCategory.Purple] = ["HYBRID 2/3 for an archetype",
                "Specialist: +3% damage and credits, +2% speed, 3% less damage taken · keystone at 5 (3/5)",
                "Specialist KEYSTONE: +8% damage and credits, +5% speed, 8% less damage taken"],
        };

        await Client.WaitAssertion(() =>
        {
            var window = new PerkShopWindow();
            var label = window.FindControl<Label>("ArchetypeLabel");

            foreach (var (category, lines) in expected)
            {
                var ids = FSPerkDef.All.Values.Where(d => d.Category == category).Select(d => d.Id)
                    .Take(FSArchetypeRules.KeystoneCount).ToArray();
                Assert.That(ids, Has.Length.EqualTo(FSArchetypeRules.KeystoneCount), "test precondition");

                string Shown(int count)
                {
                    var ev = new FSPerksStateEvent();
                    Array.Fill(ev.Slots, string.Empty);
                    for (var i = 0; i < count; i++)
                    {
                        ev.Levels[ids[i]] = 1;
                        ev.Slots[i] = ids[i];
                    }

                    window.UpdateState(ev);
                    return label.Text ?? string.Empty;
                }

                Assert.Multiple(() =>
                {
                    Assert.That(Shown(2), Is.EqualTo(lines[0]));
                    Assert.That(Shown(3), Is.EqualTo(lines[1]));
                    Assert.That(Shown(5), Is.EqualTo(lines[2]));
                });
            }

            window.Dispose();
        });
    }
}
