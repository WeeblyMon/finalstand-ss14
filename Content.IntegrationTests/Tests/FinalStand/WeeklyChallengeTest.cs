using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Discord;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Ladder;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using Robust.Server.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class WeeklyChallengeTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    [Test]
    public async Task EveryChallengeUsesRealCompatibleModifiers()
    {
        await Server.WaitAssertion(() =>
        {
            var challenges = SProtoMan.EnumeratePrototypes<FSWeeklyChallengePrototype>().ToList();
            Assert.That(challenges, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(challenges.Select(c => c.Order), Is.Unique);

            foreach (var challenge in challenges)
            {
                var modifiers = challenge.Modifiers.Select(m => SProtoMan.Index(m)).ToList();
                foreach (var a in modifiers)
                {
                    foreach (var b in modifiers.Where(b => b != a))
                        Assert.That(FSRoundModifierRules.Compatible(a, b), Is.True, $"{challenge.ID} pairs {a.ID} with {b.ID}");
                }

                Assert.That(challenge.ClearWave, Is.InRange(1, 30));
            }
        });
    }

    [Test]
    public async Task StartingTheWeeklyLocksInItsRules()
    {
        await Server.WaitAssertion(() =>
        {
            var weekly = SEntMan.System<FSWeeklyChallengeSystem>();
            var waveRule = SEntMan.System<WaveGameRuleSystem>();
            var shell = Server.ResolveDependency<IServerConsoleHost>().LocalShell;
            var challenge = weekly.GetChallenge(DateTime.UtcNow)!;

            Assert.That(weekly.StartWeekly(challenge), Is.False, "no round running");
            Assert.That(SEntMan.System<GameTicker>().StartGameRule(FSTestRules.WaveSurvival), Is.True);
            Assert.That(weekly.StartWeekly(challenge), Is.True);
            Assert.That(weekly.StartWeekly(challenge), Is.False, "the weekly can only be started once a round");

            Assert.Multiple(() =>
            {
                Assert.That(waveRule.GetDifficulty(), Is.EqualTo(challenge.Difficulty));
                Assert.That(waveRule.GetActiveModifiers().Select(m => m.ID),
                    Is.EquivalentTo(challenge.Modifiers.Select(m => m.Id)));
                Assert.That(waveRule.TryGetWeekly(out var running, out var key), Is.True);
                Assert.That(running.ID, Is.EqualTo(challenge.ID));
                Assert.That(key, Is.EqualTo(FSWeeklyRules.WeekKey(DateTime.UtcNow)));
                Assert.That(waveRule.TryStartRerollVote(null, out _), Is.False, "the weekly's modifiers are fixed");
            });

            waveRule.ForceNextWave(shell);
            Assert.That(waveRule.TryGetRoundSummary(out var summary), Is.True);
            Assert.That(summary.WeeklyKey, Is.EqualTo(FSWeeklyRules.WeekKey(DateTime.UtcNow)));
        });
    }

    [Test]
    public async Task WeeklyRunsHaveTheirOwnLadderAndRewardsPayOncePerWeek()
    {
        await Server.WaitAssertion(() =>
        {
            var ladder = SEntMan.System<FSLadderSystem>();
            var store = SEntMan.System<FSPlayerDataStore>();
            store.DeleteAllRuns();

            var at = DateTime.UtcNow;
            var week = FSWeeklyRules.WeekKey(at);
            var weeklyRun = new FSRunRecord("Ada", FSDifficulty.Hard, FSPlayerBracket.Crew, 22, false, 4000, 900, at, week);
            var normalRun = weeklyRun with { Players = "Bo", Weekly = null, Difficulty = FSDifficulty.Normal };

            Assert.That(ladder.RecordRun(weeklyRun), Is.True);
            Assert.That(ladder.RecordRun(normalRun), Is.True, "standard ladders don't compete with weekly runs");
            Assert.Multiple(() =>
            {
                Assert.That(store.GetWeeklyTopRuns(week, 10).Select(r => r.Players), Is.EqualTo(new[] { "Ada" }));
                Assert.That(ladder.GetTopRuns(FSDifficulty.Hard, FSPlayerBracket.Crew), Is.Empty,
                    "a weekly run with forced modifiers must not sit on the Hard ladder");
            });

            var user = Guid.NewGuid();
            Assert.That(store.TryClaimWeeklyReward(user, week), Is.True);
            Assert.That(store.TryClaimWeeklyReward(user, week), Is.False);
            Assert.That(store.TryClaimWeeklyReward(user, "1999-W01"), Is.True, "a new week pays again");
        });
    }

    [Test]
    public async Task DiscordAnnouncesEachWeekOnce()
    {
        await Server.WaitAssertion(() =>
        {
            var discord = SEntMan.System<FSDiscordSystem>();
            var store = SEntMan.System<FSPlayerDataStore>();
            store.SetMeta("discord_weekly_open", "");
            store.SetMeta("discord_weekly_lastcall", "");

            var wednesday = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
            discord.CheckWeeklyPosts(wednesday);
            var opener = discord.LastPayload;
            Assert.That(opener, Is.Not.Null);

            discord.CheckWeeklyPosts(wednesday.AddHours(5));
            Assert.That(discord.LastPayload, Is.EqualTo(opener), "the opener posts once per week, not every minute");

            discord.CheckWeeklyPosts(new DateTime(2026, 10, 18, 19, 0, 0, DateTimeKind.Utc));
            Assert.That(discord.LastPayload, Is.Not.EqualTo(opener), "Sunday evening gets a last call");
            var lastCall = discord.LastPayload;

            discord.CheckWeeklyPosts(new DateTime(2026, 10, 18, 22, 0, 0, DateTimeKind.Utc));
            Assert.That(discord.LastPayload, Is.EqualTo(lastCall));
        });
    }
}
