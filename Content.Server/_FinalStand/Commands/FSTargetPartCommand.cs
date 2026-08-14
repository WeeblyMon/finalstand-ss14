using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared._Shitmed.Targeting;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class FSTargetPartCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    public string Command => "targetpart";
    public string Description => "Set which body part you are aiming at, without needing the targeting doll.";
    public string Help => "targetpart <part>  — e.g. targetpart LeftArm. Run with no args to list parts and show the current one.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } mob)
        {
            shell.WriteError("No attached entity for this session.");
            return;
        }

        if (!_entMan.TryGetComponent<TargetingComponent>(mob, out var targeting))
        {
            shell.WriteError("You have no TargetingComponent, so you cannot aim at parts.");
            return;
        }

        var valid = Enum.GetValues<TargetBodyPart>().Distinct().ToList();

        if (args.Length == 0)
        {
            shell.WriteLine($"Currently targeting: {targeting.Target}");
            shell.WriteLine($"Valid: {string.Join(", ", valid)}");
            return;
        }

        if (!Enum.TryParse<TargetBodyPart>(args[0], true, out var part) || !valid.Contains(part))
        {
            shell.WriteError($"Unknown part '{args[0]}'. Valid: {string.Join(", ", valid)}");
            return;
        }

        targeting.Target = part;
        _entMan.Dirty(mob, targeting);
        shell.WriteLine($"Now targeting: {part}");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(Enum.GetNames<TargetBodyPart>(), "<part>")
            : CompletionResult.Empty;
    }
}
