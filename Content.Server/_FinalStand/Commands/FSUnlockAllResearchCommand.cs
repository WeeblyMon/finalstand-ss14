using Content.Server._FinalStand.Research;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class FSUnlockAllResearchCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    public string Command => "fsunlockalltech";
    public string Description => "DEBUG: instantly unlocks every SCI research node (station-wide), including every weapon shop.";
    public string Help => "fsunlockalltech — no arguments, bypasses cost/materials/prerequisites";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var count = _entMan.System<FSResearchSystem>().UnlockAllNodes();
        shell.WriteLine($"Unlocked {count} research node(s).");
    }
}
