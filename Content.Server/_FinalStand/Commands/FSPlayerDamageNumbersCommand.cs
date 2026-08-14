using Content.Server._FinalStand.WaveHud;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class FSPlayerDamageNumbersCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entMan = default!;

    public string Command => "fsdmgnumbers";
    public string Description => "DEBUG: toggle damage number overlays on players.";
    public string Help => "fsdmgnumbers  — toggles player damage number display on/off for all connected clients";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _entMan.System<FSDamageNumberServerSystem>().TogglePlayerDamageNumbers(shell);
    }
}
