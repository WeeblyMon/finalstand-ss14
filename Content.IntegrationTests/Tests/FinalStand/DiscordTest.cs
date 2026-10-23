using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Discord;
using Content.Server._FinalStand.Ladder;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class DiscordTest : GameTest
{
    private static readonly DateTime At = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ARecordVictoryReportCarriesEverythingTheChannelNeeds()
    {
        await Server.WaitAssertion(() =>
        {
            var run = new FSRunRecord("Ada, Bo", FSDifficulty.Hard, FSPlayerBracket.Squad, 30, true, 5025, 2400, At);
            var previous = run with { Players = "Cy", Victory = false, Waves = 22 };
            var payload = FSDiscordMessages.RoundSummary(new FSDiscordMessages.RoundReport(
                "Final Stand EU", 42, run,
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
    public async Task ANearMissIsCalledOut()
    {
        await Server.WaitAssertion(() =>
        {
            var holder = new FSRunRecord("Vex", FSDifficulty.Normal, FSPlayerBracket.Crew, 28, false, 7000, 3000, At);
            var run = holder with { Players = "Rook", Waves = 26, EndedAtUtc = At.AddDays(1) };
            var payload = FSDiscordMessages.RoundSummary(new FSDiscordMessages.RoundReport(
                "S", 1, run, null, null, Array.Empty<(FSRunRecord, FSRunRecord)>(),
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
}
