using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class LateJoinEarnedTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task ALateJoinersCatchUpCountsAsEarned()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival), Is.True);
            var wave = SEntMan.System<WaveGameRuleSystem>().ForceNextWave(4);
            var rewards = SEntMan.EntityQuery<FSWaveRewardsComponent>().Single();
            var catchUp = rewards.CrewPaidSoFar + rewards.CatchUpPerWave * (wave!.Value - 1);
            Assert.That(catchUp, Is.GreaterThan(0), "test precondition");

            var session = ServerSession!;
            var minds = SEntMan.System<SharedMindSystem>();
            var body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var mindId = minds.CreateMind(session.UserId, "Latecomer");
            minds.TransferTo(mindId, body, mind: SEntMan.GetComponent<MindComponent>(mindId));

            // Every spawn handler runs in its real order: the catch-up is paid on this same event.
            var spawned = new PlayerSpawnCompleteEvent(body, session, null, true, true, 0, EntityUid.Invalid,
                HumanoidCharacterProfile.DefaultWithSpecies());
            SEntMan.EventBus.RaiseEvent(EventSource.Local, spawned);

            var entry = SEntMan.System<FSLevelingSystem>().GetLeaderboardSnapshot().Single(e => e.Name == "Latecomer");
            Assert.That(entry.Credits, Is.EqualTo(catchUp), "the scoreboard's earned column includes the catch-up pay");
        });
    }
}
