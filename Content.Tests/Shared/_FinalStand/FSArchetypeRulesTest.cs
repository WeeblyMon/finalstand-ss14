using System.Collections.Generic;
using System.Linq;
using Content.Shared._FinalStand.Perks;
using NUnit.Framework;

namespace Content.Tests.Shared._FinalStand;

[TestFixture]
[TestOf(typeof(FSArchetypeRules))]
public sealed class FSArchetypeRulesTest
{
    private static FSArchetype Resolve(params PerkCategory[] slotted) => FSArchetypeRules.Resolve(slotted);

    [Test]
    public void ThreeOfACategoryIsAnArchetypeAndFiveIsAKeystone()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Resolve().Active, Is.False);
            Assert.That(Resolve(PerkCategory.Red, PerkCategory.Red, PerkCategory.Blue).Active, Is.False);
            Assert.That(Resolve(PerkCategory.Red, PerkCategory.Red, PerkCategory.Red),
                Is.EqualTo(new FSArchetype(PerkCategory.Red, FSArchetypeTier.Archetype, 3)));
            Assert.That(Resolve(PerkCategory.Blue, PerkCategory.Blue, PerkCategory.Blue, PerkCategory.Blue, PerkCategory.Blue, PerkCategory.Red),
                Is.EqualTo(new FSArchetype(PerkCategory.Blue, FSArchetypeTier.Keystone, 5)));
        });
    }

    [Test]
    public void AThreeThreeSplitPicksTheSameArchetypeEveryTime()
    {
        var a = Resolve(PerkCategory.Yellow, PerkCategory.Green, PerkCategory.Yellow, PerkCategory.Green, PerkCategory.Yellow, PerkCategory.Green);
        var b = Resolve(PerkCategory.Green, PerkCategory.Yellow, PerkCategory.Green, PerkCategory.Yellow, PerkCategory.Green, PerkCategory.Yellow);
        Assert.That(a, Is.EqualTo(b));
        Assert.That(a.Category, Is.EqualTo(PerkCategory.Green));
    }

    [Test]
    public void OnlyOwnedKnownSlottedPerksCount()
    {
        var reds = FSPerkDef.All.Values.Where(d => d.Category == PerkCategory.Red).Select(d => d.Id).Take(3).ToArray();
        Assert.That(reds, Has.Length.EqualTo(3));

        var levels = new Dictionary<string, int> { [reds[0]] = 1, [reds[1]] = 4, [reds[2]] = 0 };
        var slots = new[] { reds[0], reds[1], reds[2], "NotAPerk", "", "" };
        Assert.That(FSArchetypeRules.Resolve(slots, id => levels.GetValueOrDefault(id)).Active, Is.False,
            "an unowned perk and a bogus id don't count");

        levels[reds[2]] = 2;
        Assert.That(FSArchetypeRules.Resolve(slots, id => levels.GetValueOrDefault(id)).Tier, Is.EqualTo(FSArchetypeTier.Archetype));
    }

    [Test]
    public void KeystonesAlwaysBeatTheArchetypeAndNothingIsANerf()
    {
        foreach (var category in System.Enum.GetValues<PerkCategory>())
        {
            var archetype = FSArchetypeRules.Bonus(new FSArchetype(category, FSArchetypeTier.Archetype, 3));
            var keystone = FSArchetypeRules.Bonus(new FSArchetype(category, FSArchetypeTier.Keystone, 5));
            Assert.Multiple(() =>
            {
                Assert.That(archetype, Is.Not.EqualTo(FSArchetypeBonus.None), category.ToString());
                Assert.That(keystone.Damage, Is.GreaterThanOrEqualTo(archetype.Damage));
                Assert.That(keystone.Speed, Is.GreaterThanOrEqualTo(archetype.Speed));
                Assert.That(keystone.Credits, Is.GreaterThanOrEqualTo(archetype.Credits));
                Assert.That(keystone.DamageTaken, Is.LessThanOrEqualTo(archetype.DamageTaken));
                Assert.That(archetype.Damage, Is.GreaterThanOrEqualTo(1f));
                Assert.That(archetype.DamageTaken, Is.LessThanOrEqualTo(1f));
            });
        }

        Assert.That(FSArchetypeRules.Bonus(default), Is.EqualTo(FSArchetypeBonus.None));
    }
}
