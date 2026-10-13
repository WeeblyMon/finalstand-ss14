using System.Collections.Generic;
using System.Linq;
using Content.Client._FinalStand.Ladder;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Ladder;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Server.Console;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class LadderTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task RunsAreRankedPerLadderAndRecordsAreDetected()
    {
        await Server.WaitAssertion(() =>
        {
            var ladder = SEntMan.System<FSLadderSystem>();
            SEntMan.System<FSPlayerDataStore>().DeleteAllRuns();

            var at = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
            var first = new FSRunRecord("Ada", FSDifficulty.Normal, FSPlayerBracket.Crew, 12, false, 3600, 400, at);
            var worse = first with { Players = "Bo", Waves = 8, EndedAtUtc = at.AddDays(1) };
            var win = first with { Players = "Cy", Waves = 30, Victory = true, EndedAtUtc = at.AddDays(2) };
            var otherLadder = first with { Players = "Di", Difficulty = FSDifficulty.Hard, Waves = 2, EndedAtUtc = at.AddDays(3) };

            Assert.Multiple(() =>
            {
                Assert.That(ladder.RecordRun(first), Is.True, "the first run on an empty ladder is the record");
                Assert.That(ladder.RecordRun(worse), Is.False);
                Assert.That(ladder.RecordRun(win), Is.True);
                Assert.That(ladder.RecordRun(otherLadder), Is.True, "each difficulty keeps its own ladder");
            });

            var normal = ladder.GetTopRuns(FSDifficulty.Normal, FSPlayerBracket.Crew).Select(r => r.Players);
            Assert.That(normal, Is.EqualTo(new[] { "Cy", "Ada", "Bo" }));
            Assert.That(ladder.GetTopRuns(FSDifficulty.Normal, FSPlayerBracket.Solo), Is.Empty);
        });
    }

    [Test]
    public void ARunNamesItsDefendersAndPicksTheBracket()
    {
        var summary = new WaveGameRuleSystem.RoundSummary(14, 900, false, FSDifficulty.Hard);
        var run = FSLadderSystem.BuildRun(new[] { "zed", "Amy", "bob" }, summary, 4321, DateTime.UnixEpoch);

        Assert.Multiple(() =>
        {
            Assert.That(run.Players, Is.EqualTo("Amy, bob, zed"));
            Assert.That(run.Bracket, Is.EqualTo(FSPlayerBracket.Squad));
            Assert.That(run.Waves, Is.EqualTo(14));
            Assert.That(run.Difficulty, Is.EqualTo(FSDifficulty.Hard));
            Assert.That(run.DurationSeconds, Is.EqualTo(4321));
        });

        var crowd = Enumerable.Range(0, 60).Select(i => $"Defender Number {i}").ToArray();
        Assert.That(FSLadderSystem.BuildRun(crowd, summary, 1, DateTime.UnixEpoch).Players.Length, Is.LessThanOrEqualTo(200),
            "a huge round must not write an unbounded string into every ladder row");
    }

    [Test]
    public async Task TheLadderCommandAndBoardWork()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            SEntMan.System<FSPlayerDataStore>().DeleteAllRuns();
            SEntMan.System<FSLadderSystem>().RecordRun(new FSRunRecord("Ada", FSDifficulty.Normal, FSPlayerBracket.Crew,
                30, true, 5000, 1200, DateTime.UtcNow));

            var shell = Server.ResolveDependency<IServerConsoleHost>().LocalShell;
            shell.ExecuteCommand("ladder");
            shell.ExecuteCommand("ladder hard solo");

            var board = SEntMan.SpawnEntity("FSLadderBoard", map.GridCoords);
            Assert.That(SEntMan.HasComponent<FSLadderBoardComponent>(board), Is.True);
        });
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Children)
        {
            yield return child;
            foreach (var deeper in Descendants(child))
                yield return deeper;
        }
    }

    [Test]
    public async Task OpeningTheWindowFetchesAndShowsTheRuns()
    {
        await Server.WaitPost(() =>
        {
            var store = SEntMan.System<FSPlayerDataStore>();
            store.DeleteAllRuns();
            var at = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
            SEntMan.System<FSLadderSystem>().RecordRun(
                new FSRunRecord("Ada", FSDifficulty.Normal, FSPlayerBracket.Crew, 12, false, 3600, 400, at));
        });

        var ui = Client.ResolveDependency<IUserInterfaceManager>();
        await Client.WaitPost(() => ui.GetUIController<FSLadderUIController>().Open());
        await Pair.RunTicksSync(30);

        await Client.WaitAssertion(() =>
        {
            var window = ui.WindowRoot.Children.OfType<FSLadderWindow>().Single();
            var text = Descendants(window).OfType<Label>().Select(l => l.Text ?? "").ToList();
            Assert.That(text, Has.Some.Contains("Ada"), "the first request goes out and its answer fills the window");

            window.Select(FSDifficulty.Hard, FSPlayerBracket.Crew, false);
            window.Select(FSDifficulty.Normal, FSPlayerBracket.Crew, false);
        });
        await Task.Delay(TimeSpan.FromSeconds(1));
        await Pair.RunTicksSync(30);

        await Client.WaitAssertion(() =>
        {
            var window = ui.WindowRoot.Children.OfType<FSLadderWindow>().Single();
            var text = Descendants(window).OfType<Label>().Select(l => l.Text ?? "").ToList();
            Assert.That(text, Has.Some.Contains("Ada"), "quick tab switches settle on the last tab chosen");
            window.Close();
        });
    }
}
