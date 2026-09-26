using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Interaction;
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
    public string Description => "DEBUG: spawn an entity at your feet and use it, e.g. to open a shop window.";
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

        // Wait for the client to know the entity: opening on the spawn tick opens the BUI twice.
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromMilliseconds(500), () =>
        {
            if (_entMan.Deleted(target) || _entMan.Deleted(mob))
                return;

            // Open a bound UI directly; activation checks can refuse an entity spawned on top of you.
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
