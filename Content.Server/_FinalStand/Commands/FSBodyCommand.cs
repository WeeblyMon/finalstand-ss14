using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Station;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class FSBodyCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public string Command => "fsbody";
    public string Description => "DEBUG: spawn a mob where you stand and move your mind into it.";
    public string Help => "fsbody [prototype] [startingGear]  (default MobHuman, no gear)";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { AttachedEntity: { } current } player)
        {
            shell.WriteError("No attached entity for this session.");
            return;
        }

        var protoId = args.Length > 0 ? args[0] : "MobHuman";
        if (!_proto.HasIndex<EntityPrototype>(protoId))
        {
            shell.WriteError(Help);
            return;
        }

        var minds = _entMan.System<SharedMindSystem>();
        if (!minds.TryGetMind(player.UserId, out var mindId, out var mind))
        {
            shell.WriteError("You have no mind.");
            return;
        }

        if (args.Length > 1 && !_proto.HasIndex<StartingGearPrototype>(args[1]))
        {
            shell.WriteError($"Unknown starting gear {args[1]}.");
            return;
        }

        var body = _entMan.SpawnEntity(protoId, _entMan.GetComponent<TransformComponent>(current).Coordinates);
        if (args.Length > 1)
            _entMan.System<SharedStationSpawningSystem>().EquipStartingGear(body, new ProtoId<StartingGearPrototype>(args[1]));
        minds.TransferTo(mindId.Value, body, ghostCheckOverride: true, mind: mind);
    }
}
