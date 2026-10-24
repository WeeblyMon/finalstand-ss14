using System.Linq;
using Content.Server.Discord;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;

namespace Content.Server._FinalStand.Discord;

public static class FSDiscordMessages
{
    public const int VictoryColor = 0xFFD700;
    public const int LossColor = 0xB71C1C;
    public const int RecordColor = 0xFF7043;
    public const int LiveColor = 0x43A047;
    public const double NearlyBeatenFraction = 0.9;
    public const double RareTitleShare = 0.05;
    private const int MaxFieldLength = 1000;

    public readonly record struct Mvp(string Name, int Kills, int Revives);

    public readonly record struct RoundReport(
        string ServerName,
        int RoundId,
        FSRunRecord Run,
        Mvp? TopKiller,
        Mvp? TopMedic,
        IReadOnlyList<(FSRunRecord Run, FSRunRecord? Previous)> Records,
        IReadOnlyList<(string Player, string Title)> RareTitles,
        FSRunRecord? LadderHolder);

    public static WebhookPayload RoundSummary(RoundReport report)
    {
        var run = report.Run;
        var fields = new List<WebhookEmbedField>
        {
            Field(Loc.GetString("fs-discord-field-result"), run.Victory
                ? Loc.GetString("fs-discord-result-victory")
                : Loc.GetString("fs-discord-result-fell", ("waves", run.Waves))),
            Field(Loc.GetString("fs-discord-field-difficulty"), LadderName(run.Difficulty, run.Bracket)),
            Field(Loc.GetString("fs-discord-field-time"), TimeSpan.FromSeconds(run.DurationSeconds).ToString(@"h\:mm\:ss")),
            Field(Loc.GetString("fs-discord-field-kills"), run.Kills.ToString("N0")),
            Field(Loc.GetString("fs-discord-field-defenders"), run.Players, inline: false),
        };

        if (report.TopKiller is { } killer)
            fields.Add(Field(Loc.GetString("fs-discord-field-mvp"), Loc.GetString("fs-discord-mvp-kills", ("name", killer.Name), ("kills", killer.Kills))));
        if (report.TopMedic is { Revives: > 0 } medic)
            fields.Add(Field(Loc.GetString("fs-discord-field-medic"), Loc.GetString("fs-discord-mvp-revives", ("name", medic.Name), ("revives", medic.Revives))));

        foreach (var (record, previous) in report.Records)
            fields.Add(Field(Loc.GetString("fs-discord-field-record"), DescribeRecord(record, previous), inline: false));

        if (report.RareTitles.Count > 0)
        {
            fields.Add(Field(Loc.GetString("fs-discord-field-rare-titles"),
                string.Join("\n", report.RareTitles.Select(t => Loc.GetString("fs-discord-rare-title", ("player", t.Player), ("title", t.Title)))),
                inline: false));
        }

        if (IsNearlyBeaten(run, report.LadderHolder))
        {
            fields.Add(Field(Loc.GetString("fs-discord-field-nearly"), Loc.GetString("fs-discord-nearly-beaten",
                ("short", report.LadderHolder!.Waves - run.Waves),
                ("holder", report.LadderHolder.Players),
                ("waves", report.LadderHolder.Waves)), inline: false));
        }

        var embed = new WebhookEmbed
        {
            Title = Loc.GetString(run.Victory ? "fs-discord-title-victory" : "fs-discord-title-fell", ("waves", run.Waves)),
            Color = report.Records.Count > 0 ? RecordColor : run.Victory ? VictoryColor : LossColor,
            Fields = fields,
            Footer = new WebhookEmbedFooter
            {
                Text = Loc.GetString("fs-discord-footer", ("server", report.ServerName), ("round", report.RoundId)),
            },
        };

        return new WebhookPayload { Embeds = [embed] };
    }

    public static WebhookPayload ServerLive(string serverName, int players, int wave)
    {
        var embed = new WebhookEmbed
        {
            Title = Loc.GetString("fs-discord-live-title", ("players", players)),
            Description = wave > 0
                ? Loc.GetString("fs-discord-live-wave", ("wave", wave))
                : Loc.GetString("fs-discord-live-lobby"),
            Color = LiveColor,
            Fields = new List<WebhookEmbedField>(),
            Footer = new WebhookEmbedFooter { Text = serverName },
        };
        return new WebhookPayload { Embeds = [embed] };
    }

    public const int WeeklyColor = 0xFFB300;

    public static WebhookPayload WeeklyPost(bool lastCall, string name, string description, int clearWave, FSRunRecord? leader)
    {
        var embed = new WebhookEmbed
        {
            Title = Loc.GetString(lastCall ? "fs-discord-weekly-lastcall-title" : "fs-discord-weekly-open-title", ("name", name)),
            Description = lastCall
                ? Loc.GetString("fs-discord-weekly-lastcall-desc")
                : Loc.GetString("fs-discord-weekly-open-desc", ("description", description), ("wave", clearWave)),
            Color = WeeklyColor,
            Fields = new List<WebhookEmbedField>
            {
                Field(Loc.GetString("fs-discord-field-record"), leader == null
                    ? Loc.GetString("fs-discord-weekly-no-leader")
                    : Loc.GetString("fs-discord-weekly-leader", ("players", leader.Players), ("waves", leader.Waves)), inline: false),
            },
        };
        return new WebhookPayload { Embeds = [embed] };
    }

    public static bool IsNearlyBeaten(FSRunRecord run, FSRunRecord? holder)
        => holder != null
           && !FSLadder.Beats(run, holder)
           && run.Waves < holder.Waves
           && run.Waves >= holder.Waves * NearlyBeatenFraction;

    public static bool IsRareTitle(int holders, int knownPlayers)
        => knownPlayers > 0 && holders <= Math.Max(1, (int) Math.Floor(knownPlayers * RareTitleShare));

    private static string DescribeRecord(FSRunRecord record, FSRunRecord? previous)
    {
        var ladder = record.Weekly != null
            ? Loc.GetString("fs-discord-weekly-ladder")
            : LadderName(record.Difficulty, record.Bracket);
        return previous == null
            ? Loc.GetString("fs-discord-record-first", ("ladder", ladder), ("players", record.Players))
            : Loc.GetString("fs-discord-record-beaten", ("ladder", ladder), ("players", record.Players), ("previous", previous.Players));
    }

    private static string LadderName(FSDifficulty difficulty, FSPlayerBracket bracket)
        => Loc.GetString("fs-discord-ladder-name",
            ("difficulty", Loc.GetString(FSDifficultyRules.NameLocId(difficulty))),
            ("bracket", Loc.GetString(FSLadder.BracketLocId(bracket))));

    private static WebhookEmbedField Field(string name, string value, bool inline = true) => new()
    {
        Name = name,
        Value = value.Length > MaxFieldLength ? value[..(MaxFieldLength - 1)] + "…" : value,
        Inline = inline,
    };
}
