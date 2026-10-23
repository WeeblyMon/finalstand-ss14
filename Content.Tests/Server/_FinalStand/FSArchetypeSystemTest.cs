using System;
using System.Linq;
using Content.Server._FinalStand.Perks;
using Content.Shared._FinalStand.Perks;
using NUnit.Framework;

namespace Content.Tests.Server._FinalStand;

// The server counts archetypes without allocating; it has to agree with the shared rule the shop and HUD quote.
[TestFixture]
[TestOf(typeof(FSArchetypeSystem))]
public sealed class FSArchetypeSystemTest
{
    [Test]
    public void MatchesTheSharedRule()
    {
        var ids = FSPerkDef.All.Keys.Append("NotAPerk").Append("").ToArray();
        var random = new Random(1234);

        for (var run = 0; run < 5000; run++)
        {
            var perks = new FSPerkLevelsComponent();
            for (var i = 0; i < FSPerkDef.SlotCount; i++)
            {
                // Bias towards few categories so ties and keystones come up often.
                var id = ids[random.Next(random.Next(2) == 0 ? Math.Min(12, ids.Length) : ids.Length)];
                perks.Slots[i] = random.Next(20) == 0 ? null! : id;
                if (random.Next(4) != 0 && !string.IsNullOrEmpty(id))
                    perks.Levels[id] = random.Next(FSPerkDef.MaxLevel + 1);
            }

            Assert.That(FSArchetypeSystem.Get(perks), Is.EqualTo(FSArchetypeRules.Resolve(perks.Slots, perks.GetLevel)),
                $"slots: {string.Join(", ", perks.Slots)}");
        }
    }

    [Test]
    public void ATieGoesToTheLowerCategory()
    {
        var perks = new FSPerkLevelsComponent();
        var greens = FSPerkDef.All.Values.Where(d => d.Category == PerkCategory.Green).Take(3).ToArray();
        var reds = FSPerkDef.All.Values.Where(d => d.Category == PerkCategory.Red).Take(3).ToArray();
        var slotted = greens.Concat(reds).ToArray();
        for (var i = 0; i < slotted.Length; i++)
        {
            perks.Slots[i] = slotted[i].Id;
            perks.Levels[slotted[i].Id] = 1;
        }

        Assert.That(FSArchetypeSystem.Get(perks), Is.EqualTo(new FSArchetype(PerkCategory.Red, FSArchetypeTier.Archetype, 3)));
    }
}
