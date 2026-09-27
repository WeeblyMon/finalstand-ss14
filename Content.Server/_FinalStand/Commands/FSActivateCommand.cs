using Content.Server.Administration;
using Content.Shared.Access.Components;
using Content.Shared.Administration;
using Content.Shared.Interaction;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class FSActivateCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public string Command => "fsactivate";
    public string Description => "DEBUG: spawn an entity at your feet, self-powered and unlocked, and open its UI.";
    public string Help => "fsactivate <prototype>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } mob)
        {
            shell.WriteError("No attached entity for this session.");
            return;
        }

        if (args.Length != 1 || !_proto.HasIndex<EntityPrototype>(args[0]))
        {
            shell.WriteError(Help);
            return;
        }

        var target = _entMan.SpawnEntity(args[0], _entMan.GetComponent<TransformComponent>(mob).Coordinates);
        // Consoles close their UI when unpowered; a debug spawn has no grid power to draw from.
        _entMan.System<SharedPowerReceiverSystem>().SetNeedsPower(target, false);
        _entMan.RemoveComponent<AccessReaderComponent>(target);
        shell.WriteLine($"Spawned {_entMan.ToPrettyString(target)}.");

        // Opening on the spawn tick double-opens the BUI on the client.
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromMilliseconds(500), () =>
        {
            if (_entMan.Deleted(target) || _entMan.Deleted(mob))
                return;

            if (_entMan.TryGetComponent<ActivatableUIComponent>(target, out var activatable) && activatable.Key != null)
            {
                _entMan.System<SharedUserInterfaceSystem>().OpenUi(target, activatable.Key, mob);
                return;
            }

            _entMan.System<SharedInteractionSystem>().InteractionActivate(mob, target, checkCanInteract: false, checkUseDelay: false);
        });
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<EntityPrototype>(proto: _proto), "<prototype>")
            : CompletionResult.Empty;
    }
}
