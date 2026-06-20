using System.Text;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Humanoid;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;

namespace Content.Server.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class DumpHumanoidServerCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _ent = default!;

    public string Command => "dumphumanoidserver";
    public string Description => "Server-side dump of a humanoid's MarkingSet + appearance fields.";
    public string Help => "dumphumanoidserver [netEntityId]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        EntityUid target;
        if (args.Length >= 1 && NetEntity.TryParse(args[0], out var net) && _ent.TryGetEntity(net, out var u))
        {
            target = u.Value;
        }
        else if (shell.Player?.AttachedEntity is { } attached)
        {
            target = attached;
        }
        else
        {
            shell.WriteError("No target. Pass a NetEntity or be attached to one.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"=== SERVER dumphumanoid {_ent.ToPrettyString(target)} ===");

        if (_ent.TryGetComponent<HumanoidAppearanceComponent>(target, out var hum))
        {
            sb.AppendLine($"species={hum.Species} sex={hum.Sex} gender={hum.Gender} age={hum.Age}");
            sb.AppendLine($"skinColor={hum.SkinColor} eyeColor={hum.EyeColor}");
            sb.AppendLine($"MarkingSet categories: {hum.MarkingSet.Markings.Count}");
            foreach (var (cat, list) in hum.MarkingSet.Markings)
                sb.AppendLine($"  {cat}: {string.Join(",", list.ConvertAll(m => m.MarkingId))}");
            sb.AppendLine($"HiddenLayers ({hum.HiddenLayers.Count}): {string.Join(",", hum.HiddenLayers.Keys)}");
            sb.AppendLine($"PermanentlyHidden ({hum.PermanentlyHidden.Count}): {string.Join(",", hum.PermanentlyHidden)}");
            sb.AppendLine($"CustomBaseLayers ({hum.CustomBaseLayers.Count}): {string.Join(",", hum.CustomBaseLayers.Keys)}");
            sb.AppendLine($"Initial={hum.Initial}");
        }
        else
        {
            sb.AppendLine("No HumanoidAppearanceComponent on entity.");
        }

        shell.WriteLine(sb.ToString());
    }
}
