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

    // The effect line quotes the bonus it pays, as whole percentages.
    [TestCase(PerkCategory.Red, FSArchetypeTier.Archetype, "damage", "8")]
    [TestCase(PerkCategory.Red, FSArchetypeTier.Keystone, "damage", "20")]
    [TestCase(PerkCategory.Blue, FSArchetypeTier.Archetype, "resist", "8")]
    [TestCase(PerkCategory.Blue, FSArchetypeTier.Keystone, "resist", "20")]
    [TestCase(PerkCategory.Green, FSArchetypeTier.Archetype, "speed", "6")]
    [TestCase(PerkCategory.Green, FSArchetypeTier.Keystone, "speed", "15")]
    [TestCase(PerkCategory.Yellow, FSArchetypeTier.Archetype, "credits", "12")]
    [TestCase(PerkCategory.Yellow, FSArchetypeTier.Keystone, "credits", "30")]
    [TestCase(PerkCategory.Purple, FSArchetypeTier.Archetype, "damage", "3")]
    [TestCase(PerkCategory.Purple, FSArchetypeTier.Archetype, "speed", "2")]
    [TestCase(PerkCategory.Purple, FSArchetypeTier.Archetype, "resist", "3")]
    [TestCase(PerkCategory.Purple, FSArchetypeTier.Keystone, "damage", "8")]
    [TestCase(PerkCategory.Purple, FSArchetypeTier.Keystone, "speed", "5")]
    [TestCase(PerkCategory.Purple, FSArchetypeTier.Keystone, "resist", "8")]
    public void EffectArgsQuoteTheBonus(PerkCategory category, FSArchetypeTier tier, string arg, string expected)
    {
        var args = FSArchetypeRules.EffectArgs(FSArchetypeRules.Bonus(new FSArchetype(category, tier, 5)))
            .ToDictionary(a => a.Item1, a => a.Item2);
        Assert.That(args[arg], Is.EqualTo(expected));

        // Specialist text says "damage and credits" off the one number.
        if (category == PerkCategory.Purple)
            Assert.That(args["credits"], Is.EqualTo(args["damage"]));
    }
}
