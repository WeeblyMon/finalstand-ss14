using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Content.Shared._FinalStand.Titles;

namespace Content.Server._FinalStand.Mentor;

[AnyCommand]
public sealed partial class FSMentorCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override string Command => "mentor";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var mentor = _entMan.System<FSMentorSystem>();
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("cmd-mentor-no-player"));
            return;
        }

        if (args.Length == 2 && args[0].Equals("buy", StringComparison.OrdinalIgnoreCase))
        {
            mentor.TryBuy(player.UserId, args[1], out var message);
            shell.WriteLine(message);
            return;
        }

        var record = mentor.CurrentRecord(player.UserId.UserId);
        shell.WriteLine(Loc.GetString("cmd-mentor-summary",
            ("balance", record.Balance),
            ("total", record.Total),
            ("week", record.WeekPoints),
            ("weekCap", FSMentorLedger.WeekCap),
            ("eligible", mentor.IsEligibleMentor(player) ? "yes" : "no")));

        foreach (var item in FSMentorSystem.Shop)
        {
            shell.WriteLine(Loc.GetString("cmd-mentor-shop-row",
                ("id", item.Id),
                ("cost", item.Cost),
                ("title", Loc.GetString(_proto.Index<FSTitlePrototype>(item.Title).Name))));
        }
    }
}
