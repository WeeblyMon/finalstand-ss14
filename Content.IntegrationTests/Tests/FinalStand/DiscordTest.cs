using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Discord;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Ladder;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using Content.Shared.GameTicking;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class DiscordTest : GameTest
{
    private static readonly DateTime At = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    // Test servers have no webhook. A timer or process hook would keep every discarded server alive (a stopping
    // server never calls Shutdown), which ran the full suite out of memory.
    [Test]
    public async Task AServerWithoutAWebhookHoldsNoProcessHooks()
    {
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<FSDiscordStatusSystem>().HoldsProcessHooks, Is.False));
    }

    // Two servers run from one folder share a database. Keyed by webhook alone, both edited one message, so the
    // channel only showed whichever server wrote last.
    [Test]
    public async Task EachServerKeepsItsOwnStatusMessage()
    {
        var status = SEntMan.System<FSDiscordStatusSystem>();
        var store = SEntMan.System<FSPlayerDataStore>();
        const string webhook = "test-webhook";

        await Server.WaitAssertion(() =>
        {
            store.SetMeta("discord_status_message:" + webhook, "123");
            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(status.LoadMessageId(webhook, 1212), Is.EqualTo(123ul), "the first server keeps the old shared message");
                    Assert.That(status.LoadMessageId(webhook, 1213), Is.Null, "the second server posts a message of its own");
                    Assert.That(status.LoadMessageId(webhook, 1212), Is.EqualTo(123ul), "the first server's message is stored under its port");
                });
            }
            finally
            {
                store.SetMeta("discord_status_message:" + webhook, "");
                store.SetMeta($"discord_status_message:{webhook}:1212", "");
                store.SetMeta($"discord_status_message:{webhook}:1213", "");
            }
        });
    }

    // At process exit the server has already cleared its settings, so the offline message is built from the last
    // live report instead of reading them again.
    [Test]
    public async Task TheOfflineReportKeepsWhatTheLastLiveReportSaid()
    {
        var status = SEntMan.System<FSDiscordStatusSystem>();
        var cfg = Server.ResolveDependency<IConfigurationManager>();

        await Server.WaitAssertion(() =>
        {
            var name = cfg.GetCVar(CVars.GameHostName);
            var live = status.BuildReport(offline: false);
            cfg.SetCVar(CVars.GameHostName, "renamed after the last report");
            try
            {
                var offline = status.OfflineReport();
                Assert.Multiple(() =>
                {
                    Assert.That(offline.Phase, Is.EqualTo(FSDiscordMessages.StatusPhase.Offline));
                    Assert.That(offline.ServerName, Is.EqualTo(live.ServerName), "the offline report read the settings again");
                    Assert.That(offline.Players, Is.Zero);
                });
            }
            finally
            {
                cfg.SetCVar(CVars.GameHostName, name);
            }
        });
    }

    [Test]
    public async Task ARecordVictoryReportCarriesEverythingTheChannelNeeds()
    {
        await Server.WaitAssertion(() =>
        {
            var run = new FSRunRecord("Ada, Bo", FSDifficulty.Hard, FSPlayerBracket.Squad, 30, true, 5025, 2400, At);
            var previous = run with { Players = "Cy", Victory = false, Waves = 22 };
            var payload = FSDiscordMessages.RoundSummary(new FSDiscordMessages.RoundReport(
                "Final Stand EU", 42, run, 30,
                new FSDiscordMessages.Mvp("Ada", 1300, 2),
                new FSDiscordMessages.Mvp("Bo", 200, 9),
                new[] { (run, (FSRunRecord) previous) },
                new[] { ("Ada", "Nightmare Breaker") },
                previous));

            var embed = payload.Embeds!.Single();
            var text = string.Join("\n", embed.Fields.Select(f => $"{f.Name}: {f.Value}"));
            Assert.Multiple(() =>
            {
                Assert.That(embed.Title, Is.Not.Empty);
                Assert.That(embed.Color, Is.EqualTo(FSDiscordMessages.RecordColor), "a record outranks a plain victory colour");
                Assert.That(text, Does.Contain("Ada, Bo"));
                Assert.That(text, Does.Contain("Hard · 2-4 players"), "the ladder is named by difficulty and bracket");
                Assert.That(text, Does.Contain("1300").Or.Contain("1,300"), "the MVP's kills are named");
                Assert.That(text, Does.Contain("Cy"), "the old record holder is named, which is what pulls them back");
                Assert.That(text, Does.Contain("Nightmare Breaker"));
                Assert.That(embed.Footer!.Value.Text, Does.Contain("42"));
                Assert.That(embed.Fields.All(f => f.Value.Length <= 1000), Is.True, "Discord rejects fields over 1024 characters");
                Assert.That(text, Does.Not.Contain("fs-"), "every string must be localized, no raw keys");
            });
        });
    }

    [Test]
    public async Task AnEndlessWinNamesTheWaveItWasWonAt()
    {
        await Server.WaitAssertion(() =>
        {
            var run = new FSRunRecord("Ada", FSDifficulty.Normal, FSPlayerBracket.Solo, 37, true, 9000, 5000, At);
            var payload = FSDiscordMessages.RoundSummary(new FSDiscordMessages.RoundReport(
                "S", 1, run, 30, null, null, Array.Empty<(FSRunRecord, FSRunRecord)>(),
                Array.Empty<(string, string)>(), null));

            var result = payload.Embeds!.Single().Fields[0].Value;
            Assert.That(result, Is.EqualTo("Victory at wave 30"), "endless waves after the win don't move the victory wave");
        });
    }

    [Test]
    public async Task ANearMissIsCalledOut()
    {
        await Server.WaitAssertion(() =>
        {
            var holder = new FSRunRecord("Vex", FSDifficulty.Normal, FSPlayerBracket.Crew, 28, false, 7000, 3000, At);
            var run = holder with { Players = "Rook", Waves = 26, EndedAtUtc = At.AddDays(1) };
            var payload = FSDiscordMessages.RoundSummary(new FSDiscordMessages.RoundReport(
                "S", 1, run, null, null, null, Array.Empty<(FSRunRecord, FSRunRecord)>(),
                Array.Empty<(string, string)>(), holder));

            var text = string.Join("\n", payload.Embeds!.Single().Fields.Select(f => f.Value));
            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Contain("Vex"));
                Assert.That(text, Does.Contain("2 waves"));
                Assert.That(payload.Embeds!.Single().Color, Is.EqualTo(FSDiscordMessages.LossColor));
            });
        });
    }

    [Test]
    public async Task TheRoundSummaryIsBuiltFromTheRecordedRun()
    {
        await Server.WaitAssertion(() =>
        {
            var discord = SEntMan.System<FSDiscordSystem>();
            var before = discord.LastPayload;
            var run = new FSRunRecord("Ada", FSDifficulty.Normal, FSPlayerBracket.Solo, 9, false, 1800, 300, At);
            var recorded = new FSRunRecordedEvent(run, null, true);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref recorded);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundEndMessageEvent(
                "", "", TimeSpan.Zero, 1, 0, Array.Empty<RoundEndMessageEvent.RoundEndPlayerInfo>(), null));

            Assert.That(discord.LastPayload, Is.Not.Null, "with no webhook configured the payload is still built, just not sent");
            Assert.That(discord.LastPayload, Is.Not.EqualTo(before), "a fresh payload is built for this round");
            Assert.That(discord.LastPayload!.Value.Embeds!.Single().Fields.Any(f => f.Value.Contains("Ada")), Is.True);
        });
    }

    [Test]
    public async Task TheStatusMessageShowsTheLivePopulationAndWave()
    {
        await Server.WaitAssertion(() =>
        {
            var payload = FSDiscordMessages.ServerStatus(new FSDiscordMessages.StatusReport(
                "Final Stand EU", FSDiscordMessages.StatusPhase.Combat, 21, 40, 7, false, FSDifficulty.Hard,
                new[] { "Frenzy" }, At, "ss14://finalstand.example"));

            var embed = payload.Embeds!.Single();
            var text = string.Join("\n", embed.Fields.Select(f => $"{f.Name}: {f.Value}"));
            Assert.Multiple(() =>
            {
                Assert.That(embed.Title, Is.EqualTo("Final Stand EU"));
                Assert.That(text, Does.Contain("21/40"), "the player count reads like a server browser");
                Assert.That(text, Does.Contain("7"), "the wave being fought is named");
                Assert.That(text, Does.Contain("Hard"));
                Assert.That(text, Does.Contain("Frenzy"));
                Assert.That(text, Does.Contain($"<t:{new DateTimeOffset(At).ToUnixTimeSeconds()}:R>"),
                    "the round age is a Discord timestamp so it ticks without edits");
                Assert.That(text, Does.Contain("ss14://finalstand.example"));
                Assert.That(embed.Color, Is.EqualTo(FSDiscordMessages.LiveColor));
            });
        });
    }

    [Test]
    public async Task TheLobbyAndOfflineStatusLeaveOutRoundDetails()
    {
        await Server.WaitAssertion(() =>
        {
            var lobby = FSDiscordMessages.ServerStatus(new FSDiscordMessages.StatusReport(
                "Final Stand EU", FSDiscordMessages.StatusPhase.Lobby, 3, 40, 0, false, FSDifficulty.Hard,
                new[] { "Frenzy" }, At, "ss14://finalstand.example")).Embeds!.Single();
            var offline = SEntMan.System<FSDiscordStatusSystem>().BuildReport(offline: true);
            var offlineEmbed = FSDiscordMessages.ServerStatus(offline with { Connect = "ss14://finalstand.example" }).Embeds!.Single();

            Assert.Multiple(() =>
            {
                Assert.That(lobby.Fields.Any(f => f.Value.Contains("Frenzy") || f.Value.Contains("Hard")), Is.False,
                    "last round's settings are not shown while the lobby waits");
                Assert.That(lobby.Color, Is.EqualTo(FSDiscordMessages.StatusLobbyColor));
                Assert.That(offline.Players, Is.EqualTo(0));
                Assert.That(offline.Phase, Is.EqualTo(FSDiscordMessages.StatusPhase.Offline));
                Assert.That(offlineEmbed.Color, Is.EqualTo(FSDiscordMessages.StatusOfflineColor));
                Assert.That(offlineEmbed.Fields.Any(f => f.Value.Contains("ss14://")), Is.False,
                    "an offline server does not invite people to join");
            });
        });
    }

    [Test]
    public async Task TheLiveReportFollowsTheRound()
    {
        await Server.WaitAssertion(() =>
        {
            var report = SEntMan.System<FSDiscordStatusSystem>().BuildReport(offline: false);
            Assert.That(report.Phase, Is.Not.EqualTo(FSDiscordMessages.StatusPhase.Offline));
            Assert.That(report.Players, Is.GreaterThanOrEqualTo(0));
        });
    }

    [Test]
    public async Task TheStatusSaysWhenItWasLastCheckedOrWentOffline()
    {
        await Server.WaitAssertion(() =>
        {
            var stamp = $"<t:{new DateTimeOffset(At).ToUnixTimeSeconds()}:R>";
            var live = FSDiscordMessages.ServerStatus(new FSDiscordMessages.StatusReport(
                "Final Stand EU", FSDiscordMessages.StatusPhase.Lobby, 3, 40, 0, false, null,
                Array.Empty<string>(), null, null, At)).Embeds!.Single();
            var offline = FSDiscordMessages.ServerStatus(new FSDiscordMessages.StatusReport(
                "Final Stand EU", FSDiscordMessages.StatusPhase.Offline, 0, 40, 0, false, null,
                Array.Empty<string>(), null, null, At)).Embeds!.Single();

            Assert.Multiple(() =>
            {
                Assert.That(live.Description, Does.Contain(stamp), "a stale message shows how long ago it was checked");
                Assert.That(offline.Description, Does.Contain(stamp));
                Assert.That(offline.Description, Is.Not.EqualTo(live.Description), "offline says when it went down, not when it was checked");
            });
        });
    }
}
