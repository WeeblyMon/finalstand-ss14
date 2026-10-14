using Content.Server._FinalStand.MedicalOps;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class FSGiveMedicalFundCommand : LocalizedEntityCommands
{
    [Dependency] private FSMedicalFundSystem _fund = default!;

    public override string Command => "fsgivemedfund";
    public override string Description => "DEBUG: add credits to the Medical department fund";
    public override string Help => "fsgivemedfund <amount>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var amount) || amount <= 0)
        {
            shell.WriteError(Help);
            return;
        }

        _fund.GrantMedicalFunds(amount, "admin");
        shell.WriteLine($"Medical fund +{amount}, now {_fund.GetBalance()}.");
    }
}
