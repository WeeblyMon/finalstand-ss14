using Content.Shared._FinalStand.Mentor;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Mentor;

[AnyCommand]
public sealed partial class FSMentorCommand : LocalizedEntityCommands
{
    [Dependency] private FSMentorSystem _mentor = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override string Command => "mentor";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("cmd-mentor-no-player"));
            return;
        }

        if (args.Length == 2 && args[0].Equals("buy", StringComparison.OrdinalIgnoreCase))
        {
            _mentor.TryBuy(player.UserId, args[1], out var message);
            shell.WriteLine(message);
            return;
        }

        var record = _mentor.CurrentRecord(player.UserId.UserId);
        shell.WriteLine(Loc.GetString("cmd-mentor-summary",
            ("balance", record.Balance),
            ("total", record.Total),
            ("week", record.WeekPoints),
            ("weekCap", FSMentorLedger.WeekCap),
            ("eligible", _mentor.IsEligibleMentor(player)),
            ("hours", (int) FSMentorRules.MinPlaytime.TotalHours),
            ("level", FSMentorRules.MinLevel)));

        foreach (var item in FSMentorSystem.Shop)
        {
            shell.WriteLine(item.Title is { } title
                ? Loc.GetString("cmd-mentor-shop-row",
                    ("id", item.Id),
                    ("cost", item.Cost),
                    ("title", Loc.GetString(_proto.Index(title).Name)))
                : Loc.GetString("cmd-mentor-shop-perk-row",
                    ("id", item.Id),
                    ("cost", item.Cost),
                    ("points", item.PerkPoints)));
        }
    }
}
