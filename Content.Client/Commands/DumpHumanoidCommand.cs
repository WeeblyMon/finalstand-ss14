using System.Text;
using Content.Shared.Body.Components;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Client.Commands;

public sealed class DumpHumanoidCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _ent = default!;

    public override string Command => "dumphumanoid";
    public override string Description => "Dump sprite layers + child entities + humanoid state for the given entity (or the local player).";
    public override string Help => "dumphumanoid [entityUid]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
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
        sb.AppendLine($"=== dumphumanoid {_ent.ToPrettyString(target)} ===");

        if (_ent.TryGetComponent<TransformComponent>(target, out var xform))
        {
            sb.AppendLine($"Transform: pos={xform.LocalPosition} rot={xform.LocalRotation.Degrees:F1}° parent={(xform.ParentUid == EntityUid.Invalid ? "<none>" : _ent.ToPrettyString(xform.ParentUid))}");
            sb.AppendLine($"Children ({xform.ChildCount}):");
            var containerSys = _ent.System<SharedContainerSystem>();
            var en = xform.ChildEnumerator;
            while (en.MoveNext(out var c))
            {
                var cx = _ent.GetComponent<TransformComponent>(c);
                var inContainer = containerSys.TryGetContainingContainer((c, null), out var cont);
                var hasSprite = _ent.TryGetComponent<SpriteComponent>(c, out var childSprite);
                var hasBodyPart = _ent.TryGetComponent<OrganComponent>(c, out var bp);
                sb.Append($"  {_ent.ToPrettyString(c)} pos={cx.LocalPosition}");
                sb.Append($" inContainer={(inContainer ? cont!.ID : "NO")}");
                if (hasSprite)
                    sb.Append($" sprite[vis={childSprite!.Visible},occluded={childSprite.ContainerOccluded}]");
                if (hasBodyPart)
                    sb.Append($" Organ(Category={bp!.Category},Body={(bp.Body == null ? "null" : _ent.ToPrettyString(bp.Body.Value))})");
                sb.AppendLine();
            }
        }

        if (_ent.TryGetComponent<SpriteComponent>(target, out var sprite))
        {
            sb.AppendLine($"Sprite: visible={sprite.Visible} noRotation={sprite.NoRotation} containerOccluded={sprite.ContainerOccluded}");
            var i = 0;
            foreach (var layer in sprite.AllLayers)
            {
                var rsi = layer.ActualRsi?.Path.ToString() ?? "(no rsi)";
                var state = layer.RsiState.Name ?? "(no state)";
                sb.AppendLine($"  [{i,3}] vis={layer.Visible} rsi={rsi} state={state}");
                i++;
            }
            sb.AppendLine($"  (total layers={i})");
        }
        else
        {
            sb.AppendLine("No SpriteComponent.");
        }

        if (_ent.TryGetComponent<HumanoidProfileComponent>(target, out var hum))
        {
            sb.AppendLine($"Humanoid: species={hum.Species} sex={hum.Sex} markings={hum.MarkingSet.Markings.Count}");
            foreach (var (cat, list) in hum.MarkingSet.Markings)
                sb.AppendLine($"  marking-cat {cat}: {string.Join(",", list.ConvertAll(m => m.MarkingId))}");
            sb.AppendLine($"  HiddenLayers: {string.Join(",", hum.HiddenLayers.Keys)}");
            sb.AppendLine($"  PermanentlyHidden: {string.Join(",", hum.PermanentlyHidden)}");
            sb.AppendLine($"  CustomBaseLayers: {string.Join(",", hum.CustomBaseLayers.Keys)}");
            sb.AppendLine($"  BaseLayers: {string.Join(",", hum.BaseLayers.Keys)}");
        }

        if (_ent.TryGetComponent<BodyComponent>(target, out var body))
        {
            sb.AppendLine($"Body: organs={body.Organs?.ContainedEntities.Count ?? 0}");
        }

        shell.WriteLine(sb.ToString());
    }
}
