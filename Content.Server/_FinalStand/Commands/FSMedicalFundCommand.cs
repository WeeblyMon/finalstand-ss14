using Content.Server._FinalStand.MedicalOps;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class FSMedicalFundCommand : LocalizedEntityCommands
{
    [Dependency] private FSMedicalFundSystem _fund = default!;

    public override string Command => "fsmedfund";
    public override string Description => "DEBUG: dump the Medical department fund and its contributors";
    public override string Help => "fsmedfund";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        shell.WriteLine("=== FSMedicalFund state ===");
        _fund.DumpFund(shell);
    }
}
