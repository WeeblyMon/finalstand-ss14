using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Records;

[AnyCommand]
public sealed partial class FSRecordsCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entMan = default!;

    public override string Command => "records";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("cmd-records-no-player"));
            return;
        }

        var profile = _entMan.System<FSRecordsSystem>().GetProfile(player.UserId);
        if (profile.RoundsPlayed == 0)
        {
            shell.WriteLine(Loc.GetString("cmd-records-empty"));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-records-summary",
            ("rounds", profile.RoundsPlayed),
            ("victories", profile.Victories),
            ("wave", profile.FurthestWave),
            ("bestKills", profile.BestRoundKills),
            ("kills", profile.TotalKills),
            ("revives", profile.TotalRevives)));
    }
}
