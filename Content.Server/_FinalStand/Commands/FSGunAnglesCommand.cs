// Diagnostic: dump a held gun's real spread angles against its prototype's.
using System.Linq;
using Content.Server._FinalStand.Shop;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Administration;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Commands;

[AnyCommand]
public sealed class FSGunAnglesCommand : IConsoleCommand
{
    public string Command => "fsgunangles";
    public string Description => "Print the spread angles of every gun you carry, next to its prototype values.";
    public string Help => "fsgunangles";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var protoMan = IoCManager.Resolve<IPrototypeManager>();

        if (shell.Player is not { } session || session.AttachedEntity is not { } player)
        {
            shell.WriteError("No attached entity.");
            return;
        }

        var hands = entMan.System<SharedHandsSystem>();
        var found = 0;

        foreach (var held in hands.EnumerateHeld(player))
        {
            if (!entMan.TryGetComponent<GunComponent>(held, out var gun))
                continue;

            found++;
            var meta = entMan.GetComponent<MetaDataComponent>(held);
            var protoId = meta.EntityPrototype?.ID ?? "<none>";

            shell.WriteLine($"── {meta.EntityName}  [{protoId}]");
            shell.WriteLine($"   live  min={gun.MinAngle.Degrees:0.###}  max={gun.MaxAngle.Degrees:0.###}  inc={gun.AngleIncrease.Degrees:0.###}");
            shell.WriteLine($"   mod   min={gun.MinAngleModified.Degrees:0.###}  max={gun.MaxAngleModified.Degrees:0.###}  inc={gun.AngleIncreaseModified.Degrees:0.###}");

            if (protoMan.TryIndex<EntityPrototype>(protoId, out var proto)
                && proto.TryGetComponent<GunComponent>(out var protoGun, entMan.ComponentFactory))
            {
                shell.WriteLine($"   proto min={protoGun.MinAngle.Degrees:0.###}  max={protoGun.MaxAngle.Degrees:0.###}  inc={protoGun.AngleIncrease.Degrees:0.###}");
            }
            else
            {
                shell.WriteLine("   proto <no GunComponent on prototype>");
            }

            if (entMan.TryGetComponent<FSWeaponUpgradeStateComponent>(held, out var state))
                shell.WriteLine($"   levels {string.Join(", ", state.Levels.Select(kv => $"{kv.Key}={kv.Value}"))}");
            else
                shell.WriteLine("   levels <no upgrade state>");
        }

        if (found == 0)
            shell.WriteError("No gun in hands. Hold the weapon and run this again.");
    }
}
