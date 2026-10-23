using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Perks;
using Content.Server._FinalStand.Titles;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class PerkMasteryTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task OnlyKillsWithAMaxedSlottedPerkCount()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var mastery = SEntMan.System<FSPerkMasterySystem>();
            var minds = SEntMan.System<SharedMindSystem>();
            var session = ServerSession!;
            SEntMan.System<FSPlayerDataStore>().DeleteProgress(session.UserId.UserId);

            var body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var mindId = minds.CreateMind(session.UserId, "Killer");
            minds.TransferTo(mindId, body, mind: SEntMan.GetComponent<MindComponent>(mindId));
            var perks = SEntMan.EnsureComponent<FSPerkLevelsComponent>(mindId);
            perks.Levels["StoppingPower"] = FSPerkDef.MaxLevel;
            perks.Levels["BulletStorm"] = FSPerkDef.MaxLevel - 1;
            perks.Levels["Juggernaught"] = FSPerkDef.MaxLevel;
            perks.Slots[0] = "StoppingPower";
            perks.Slots[1] = "BulletStorm";
            perks.Invalidate();

            var before = mastery.GetMastery(session.UserId).ToDictionary(kv => kv.Key, kv => kv.Value);
            for (var i = 0; i < 3; i++)
            {
                var died = new FSWaveEnemyDiedEvent(EntityUid.Invalid, body);
                SEntMan.EventBus.RaiseEvent(EventSource.Local, ref died);
            }

            var after = mastery.GetMastery(session.UserId);
            Assert.Multiple(() =>
            {
                Assert.That(after.GetValueOrDefault("StoppingPower") - before.GetValueOrDefault("StoppingPower"), Is.EqualTo(3));
                Assert.That(after.GetValueOrDefault("BulletStorm"), Is.EqualTo(before.GetValueOrDefault("BulletStorm")),
                    "a perk below level 4 doesn't build mastery");
                Assert.That(after.GetValueOrDefault("Juggernaught"), Is.EqualTo(before.GetValueOrDefault("Juggernaught")),
                    "an owned but unslotted perk doesn't build mastery");
            });
        });
    }

    [Test]
    public async Task GoldMasteryGrantsTitlesAndPersists()
    {
        await Server.WaitAssertion(() =>
        {
            var mastery = SEntMan.System<FSPerkMasterySystem>();
            var titles = SEntMan.System<FSTitleSystem>();
            var store = SEntMan.System<FSPlayerDataStore>();
            var user = new NetUserId(Guid.NewGuid());
            store.DeleteProgress(user.UserId);

            var category = FSPerkDef.All["StoppingPower"].Category;
            var categoryPerks = FSPerkDef.All.Values.Where(p => p.Category == category).Select(p => p.Id).ToList();

            mastery.AddKills(user, "StoppingPower", FSPerkMastery.Thresholds[^1]);
            Assert.That(titles.GetOwned(user).Select(t => t.Id), Does.Contain(FSPerkMasterySystem.PerkMasterTitle.Id));
            Assert.That(titles.GetOwned(user).Select(t => t.Id), Does.Not.Contain(FSPerkMastery.CategoryTitle(category).Id));

            foreach (var perk in categoryPerks)
                mastery.AddKills(user, perk, FSPerkMastery.Thresholds[^1]);
            Assert.That(titles.GetOwned(user).Select(t => t.Id), Does.Contain(FSPerkMastery.CategoryTitle(category).Id),
                "Gold on the whole category earns the category title");

            mastery.Flush();
            Assert.That(store.GetMastery(user.UserId)["StoppingPower"], Is.EqualTo(FSPerkMastery.Thresholds[^1] * 2));
        });
    }

    [Test]
    public async Task EveryCategoryHasItsMasteryTitle()
    {
        await Server.WaitAssertion(() =>
        {
            foreach (var category in FSPerkDef.All.Values.Select(p => p.Category).Distinct())
            {
                Assert.That(SProtoMan.HasIndex<Content.Shared._FinalStand.Titles.FSTitlePrototype>(FSPerkMastery.CategoryTitle(category)),
                    Is.True, $"no title prototype for {category}");
            }
        });
    }
}
