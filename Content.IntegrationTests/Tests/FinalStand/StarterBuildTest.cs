using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Perks;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class StarterBuildTest : GameTest
{
    private static readonly ProtoId<FSStarterBuildPrototype> Rifleman = "FSStarterRifleman";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task EveryStarterBuildIsSixRealDistinctPerks()
    {
        await Server.WaitAssertion(() =>
        {
            var builds = SProtoMan.EnumeratePrototypes<FSStarterBuildPrototype>().ToList();
            Assert.That(builds, Has.Count.GreaterThanOrEqualTo(3));

            foreach (var build in builds)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(build.Perks, Has.Count.EqualTo(FSPerkDef.SlotCount), $"{build.ID} must fill every slot");
                    Assert.That(build.Perks, Is.Unique, $"{build.ID} repeats a perk");
                    foreach (var perk in build.Perks)
                        Assert.That(FSPerkDef.All.ContainsKey(perk), Is.True, $"{build.ID} names unknown perk {perk}");
                });
            }

            Assert.That(builds.Select(b => b.Order), Is.Unique, "builds need a stable display order");
        });
    }

    [Test]
    public async Task ApplyingABuildSpendsPointsAndFillsTheSlots()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var session = ServerSession!;
            var minds = SEntMan.System<SharedMindSystem>();
            var perksSys = SEntMan.System<FSPerkSystem>();
            var store = SEntMan.System<FSPlayerDataStore>();

            store.SaveLoadoutJson(session.UserId.UserId, "{}", "[]", "[]");
            SEntMan.SpawnEntity("FSPerkShop", map.GridCoords);
            var body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var mindId = minds.CreateMind(session.UserId, "Recruit");
            minds.TransferTo(mindId, body, mind: SEntMan.GetComponent<MindComponent>(mindId));
            var wallet = SEntMan.EnsureComponent<FSPlayerWalletComponent>(mindId);
            var perks = SEntMan.EnsureComponent<FSPerkLevelsComponent>(mindId);
            perks.Levels.Clear();
            Array.Fill(perks.Slots, string.Empty);
            wallet.PerkPoints = 15;

            var build = SProtoMan.Index(Rifleman);
            perksSys.ApplyStarterBuild(session, build.ID);

            Assert.Multiple(() =>
            {
                Assert.That(wallet.PerkPoints, Is.EqualTo(1), "15 PP buys six level 1s and four level 2s for 14 PP");
                Assert.That(perks.Slots, Is.EqualTo(build.Perks.ToArray()), "every build perk lands in a slot, in order");
                Assert.That(build.Perks.Count(p => perks.GetLevel(p) == 2), Is.EqualTo(4));
            });

            perksSys.ApplyStarterBuild(session, build.ID);
            Assert.That(wallet.PerkPoints, Is.EqualTo(1), "applying again with 1 PP left buys nothing more");

            perksSys.ApplyStarterBuild(session, "NotARealBuild");
            Assert.That(wallet.PerkPoints, Is.EqualTo(1), "an unknown build id is ignored");
        });
    }

    [Test]
    public async Task ABuildCannotBeAppliedAwayFromThePerkShop()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var session = ServerSession!;
            var minds = SEntMan.System<SharedMindSystem>();
            var perksSys = SEntMan.System<FSPerkSystem>();

            var body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var mindId = minds.CreateMind(session.UserId, "Wanderer");
            minds.TransferTo(mindId, body, mind: SEntMan.GetComponent<MindComponent>(mindId));
            var wallet = SEntMan.EnsureComponent<FSPlayerWalletComponent>(mindId);
            wallet.PerkPoints = 15;

            perksSys.ApplyStarterBuild(session, "FSStarterMedic");

            Assert.That(wallet.PerkPoints, Is.EqualTo(15), "the same shop range rule as buying a single perk applies");
        });
    }
}
