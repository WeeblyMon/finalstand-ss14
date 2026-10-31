#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Server._FinalStand.Perks;
using Content.Shared._FinalStand.Perks;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class PerkStorageTest
{
    // Stored perk levels are trusted no further than the catalog: unknown perks and levels outside 1..MaxLevel go.
    [Test]
    public void StoredPerkLevelsAreSanitised()
    {
        var known = FSPerkDef.All.Keys.First();
        var clean = FSPerkSystem.SanitizeLevels(new Dictionary<string, int>
        {
            { known, FSPerkDef.MaxLevel + 5 },
            { "NoSuchPerk", 2 },
        });

        Assert.Multiple(() =>
        {
            Assert.That(clean, Has.Count.EqualTo(1));
            Assert.That(clean[known], Is.EqualTo(FSPerkDef.MaxLevel));
        });

        Assert.That(FSPerkSystem.SanitizeLevels(new Dictionary<string, int> { { known, 0 } }), Is.Empty);
    }
}
