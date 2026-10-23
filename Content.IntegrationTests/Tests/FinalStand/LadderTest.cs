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

            var normal = ladder.GetTopRuns(FSDifficulty.Normal, FSPlayerBracket.Any).Select(r => r.Players);
            Assert.That(normal, Is.EqualTo(new[] { "Cy", "Ada", "Bo" }));
            Assert.That(ladder.RecordRun(first with { Players = "Ed", Bracket = FSPlayerBracket.Squad, Waves = 20 }), Is.False,
                "a smaller crew competes against the same any-size record");
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

    [Test]
    public async Task BoardsShowTheirOwnLadderToEveryoneAndUpdateLive()
    {
        var map = await Pair.CreateTestMap();
        var normal = EntityUid.Invalid;
        var hard = EntityUid.Invalid;
        var weekly = EntityUid.Invalid;
        var week = string.Empty;

        await Server.WaitPost(() =>
        {
            var store = SEntMan.System<FSPlayerDataStore>();
            store.DeleteAllRuns();
            week = FSWeeklyRules.WeekKey(DateTime.UtcNow);
            var at = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
            store.InsertRun(new FSRunRecord("Ada", FSDifficulty.Normal, FSPlayerBracket.Squad, 12, false, 3600, 400, at));
            store.InsertRun(new FSRunRecord("Bo", FSDifficulty.Normal, FSPlayerBracket.Crew, 20, false, 3600, 400, at));

            normal = SEntMan.SpawnEntity("FSLadderBoard", map.GridCoords);
            hard = SEntMan.SpawnEntity("FSLadderBoardHard", map.GridCoords);
            weekly = SEntMan.SpawnEntity("FSLadderBoardWeekly", map.GridCoords);
            if (ServerSession!.AttachedEntity is { } player)
                SEntMan.System<SharedTransformSystem>().SetCoordinates(player, map.GridCoords);
        });
        await Pair.RunTicksSync(10);

        await Client.WaitAssertion(() =>
        {
            var board = CEntMan.GetComponent<FSLadderBoardComponent>(Pair.ToClientUid(normal));
            Assert.Multiple(() =>
            {
                Assert.That(board.Runs.Select(r => r.Players), Is.EqualTo(new[] { "Bo", "Ada" }),
                    "a freshly placed board already shows its ladder, every crew size together");
                Assert.That(CEntMan.GetComponent<FSLadderBoardComponent>(Pair.ToClientUid(hard)).Runs, Is.Empty);
                Assert.That(CEntMan.GetComponent<FSLadderBoardComponent>(Pair.ToClientUid(weekly)).Runs, Is.Empty);
            });
        });

        await Server.WaitPost(() =>
        {
            var ladder = SEntMan.System<FSLadderSystem>();
            var at = DateTime.UtcNow;
            ladder.RecordRun(new FSRunRecord("Cy", FSDifficulty.Normal, FSPlayerBracket.Squad, 30, true, 5000, 900, at));
            ladder.RecordRun(new FSRunRecord("Di", FSDifficulty.Hard, FSPlayerBracket.Squad, 4, false, 900, 80, at));
            ladder.RecordRun(new FSRunRecord("Gus", FSDifficulty.Hard, FSPlayerBracket.Crew, 9, false, 900, 80, at));
            ladder.RecordRun(new FSRunRecord("Ed", FSDifficulty.Normal, FSPlayerBracket.Squad, 6, false, 900, 80, at, week));
            for (var i = 0; i < FSLadderBoardComponent.ShownRuns + 2; i++)
                ladder.RecordRun(new FSRunRecord($"Filler {i}", FSDifficulty.Normal, FSPlayerBracket.Squad, 1, false, 60, 1, at));
        });
        await Pair.RunTicksSync(10);

        await Client.WaitAssertion(() =>
        {
            var board = CEntMan.GetComponent<FSLadderBoardComponent>(Pair.ToClientUid(normal));
            var weeklyBoard = CEntMan.GetComponent<FSLadderBoardComponent>(Pair.ToClientUid(weekly));
            Assert.Multiple(() =>
            {
                Assert.That(board.Runs, Has.Count.EqualTo(FSLadderBoardComponent.ShownRuns), "the board keeps to its top rows");
                Assert.That(board.Runs.Take(2).Select(r => r.Players), Is.EqualTo(new[] { "Cy", "Bo" }),
                    "a new record goes straight to the top without anyone touching the board");
                Assert.That(CEntMan.GetComponent<FSLadderBoardComponent>(Pair.ToClientUid(hard)).Runs.Select(r => r.Players),
                    Is.EqualTo(new[] { "Gus", "Di" }), "boards show every crew size");
                Assert.That(weeklyBoard.Runs.Select(r => r.Players), Is.EqualTo(new[] { "Ed" }));
                Assert.That(weeklyBoard.WeeklyName, Is.Not.Null.And.Not.Empty, "the weekly board names this week's challenge");
            });
        });
    }

    [Test]
    public void YourOwnNameComesFirstInACrew()
    {
        var crew = "Ada, Bo, Cy";
        Assert.Multiple(() =>
        {
            Assert.That(Content.Client._FinalStand.Ladder.FSLadderBoardOverlay.CrewFor(crew, "cy"), Is.EqualTo(new[] { "Cy", "Ada", "Bo" }));
            Assert.That(Content.Client._FinalStand.Ladder.FSLadderBoardOverlay.CrewFor(crew, "Zed"), Is.EqualTo(new[] { "Ada", "Bo", "Cy" }));
            Assert.That(Content.Client._FinalStand.Ladder.FSLadderBoardOverlay.CrewFor(crew, null), Is.EqualTo(new[] { "Ada", "Bo", "Cy" }));
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

            window.Select(FSDifficulty.Hard, FSPlayerBracket.Any, false);
            window.Select(FSDifficulty.Normal, FSPlayerBracket.Any, false);
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
