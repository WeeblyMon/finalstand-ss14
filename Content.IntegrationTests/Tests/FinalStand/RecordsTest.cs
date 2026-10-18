using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Records;
using Content.Server.GameTicking;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class RecordsTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task ARespawnedPlayerIsRecordedOncePerRound()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var records = SEntMan.System<FSRecordsSystem>();
            var store = SEntMan.System<FSPlayerDataStore>();
            var minds = SEntMan.System<SharedMindSystem>();
            var session = ServerSession!;
            store.DeleteProgress(session.UserId.UserId);

            var firstLife = minds.CreateMind(session.UserId, "First Life");
            SEntMan.EnsureComponent<FSPlayerLevelComponent>(firstLife);

            var body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var secondLife = minds.CreateMind(session.UserId, "Second Life");
            minds.TransferTo(secondLife, body, mind: SEntMan.GetComponent<MindComponent>(secondLife));
            SEntMan.EnsureComponent<FSPlayerLevelComponent>(secondLife);

            var wave = new WaveEndedEvent(7);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref wave);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundEndTextAppendEvent());

            var profile = records.GetProfile(session.UserId);
            Assert.Multiple(() =>
            {
                Assert.That(profile.RoundsPlayed, Is.EqualTo(1), "two minds in one round are still one round");
                Assert.That(profile.FurthestWave, Is.EqualTo(7));
            });
        });
    }

    [Test]
    public async Task RecordsAccumulateAndOnlyImprovementsCountAsBests()
    {
        await Server.WaitAssertion(() =>
        {
            var records = SEntMan.System<FSRecordsSystem>();
            var store = SEntMan.System<FSPlayerDataStore>();
            var user = new NetUserId(Guid.NewGuid());
            store.DeleteProgress(user.UserId);

            var first = records.RecordRound(user, kills: 40, revives: 2, highestWave: 9, victory: false);
            Assert.That(first, Is.Empty, "a first round has nothing to beat, so it must not spam 'new best' lines");

            var worse = records.RecordRound(user, kills: 10, revives: 1, highestWave: 4, victory: false);
            Assert.That(worse, Is.Empty);

            var better = records.RecordRound(user, kills: 55, revives: 0, highestWave: 30, victory: true);
            Assert.That(better, Is.EquivalentTo(new[]
            {
                new FSRecordsSystem.NewBest(FSRecordsSystem.RecordKind.FurthestWave, 9, 30),
                new FSRecordsSystem.NewBest(FSRecordsSystem.RecordKind.RoundKills, 40, 55),
            }));

            var profile = records.GetProfile(user);
            Assert.Multiple(() =>
            {
                Assert.That(profile.RoundsPlayed, Is.EqualTo(3));
                Assert.That(profile.Victories, Is.EqualTo(1));
                Assert.That(profile.TotalKills, Is.EqualTo(105));
                Assert.That(profile.TotalRevives, Is.EqualTo(3));
                Assert.That(profile.FurthestWave, Is.EqualTo(30));
                Assert.That(profile.BestRoundKills, Is.EqualTo(55));
            });
        });
    }

    [Test]
    public async Task RecordingARoundKeepsTheEquippedTitle()
    {
        await Server.WaitAssertion(() =>
        {
            var records = SEntMan.System<FSRecordsSystem>();
            var store = SEntMan.System<FSPlayerDataStore>();
            var user = new NetUserId(Guid.NewGuid());
            store.DeleteProgress(user.UserId);
            store.SaveProfile(user.UserId, default(FSPlayerDataStore.ProfileRecord) with { EquippedTitle = "FSTitleSurvivor" });

            records.RecordRound(user, kills: 1, revives: 0, highestWave: 1, victory: false);

            Assert.That(records.GetProfile(user).EquippedTitle, Is.EqualTo("FSTitleSurvivor"),
                "the records write shares a row with the title and must not wipe it");
        });
    }
}
