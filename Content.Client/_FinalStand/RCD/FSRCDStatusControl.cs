using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._FinalStand.Materials;
using Content.Shared._FinalStand.RCD;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.RCD;

// Status line under the hand: build mode, have/need for each material, and cap use. Never hidden behind examine.
public sealed class FSRCDStatusControl : PollingItemStatusControl<FSRCDStatusControl.Data>
{
    private readonly Entity<FSEngineerOnlyRCDComponent> _rcd;
    private readonly IEntityManager _entMan;
    private readonly IPrototypeManager _proto;
    private readonly IPlayerManager _player;
    private readonly RichTextLabel _label;

    public FSRCDStatusControl(Entity<FSEngineerOnlyRCDComponent> rcd)
    {
        _rcd = rcd;
        _entMan = IoCManager.Resolve<IEntityManager>();
        _proto = IoCManager.Resolve<IPrototypeManager>();
        _player = IoCManager.Resolve<IPlayerManager>();
        _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };
        AddChild(_label);
        UpdateDraw();
    }

    protected override Data PollData()
    {
        if (!_entMan.TryGetComponent<RCDComponent>(_rcd, out var rcd))
            return new Data(string.Empty);

        var proto = _proto.Index(rcd.ProtoId);
        if (proto.Mode == RcdMode.Deconstruct)
            return new Data(Loc.GetString("fs-rcd-status-deconstruct"));

        var lines = new List<string> { Loc.GetString("fs-rcd-status-build", ("name", FSRCDText.Name(_proto, proto))) };
        if (_proto.TryIndex<FSBuildRecipePrototype>(rcd.ProtoId.Id, out var recipe))
        {
            lines.Add(FSRCDText.Costs(_entMan, _proto, _player.LocalEntity, recipe));
            if (recipe.CapGroup is { } group)
                lines.Add(FSRCDText.Caps(_rcd.Comp, group));
        }

        return new Data(string.Join("\n", lines));
    }

    protected override void Update(in Data data)
    {
        _label.SetMarkup(data.Markup);
    }

    public record struct Data(string Markup);
}

public static class FSRCDText
{
    private const string Enough = "#8fe07a";
    private const string Short = "#ff6b5e";

    public static string Name(IPrototypeManager protoMan, RCDPrototype proto)
    {
        var name = proto.Mode is RcdMode.ConstructObject or RcdMode.ConstructTile
                   && proto.Prototype != null
                   && protoMan.TryIndex(proto.Prototype, out EntityPrototype? entProto)
            ? entProto.Name
            : Loc.GetString(proto.SetName);

        return name.Length == 0 ? name : Concat(char.ToUpper(name[0]).ToString(), name.Substring(1));
    }

    // Separate method so Roslyn doesn't fold it into a call the sandbox rejects.
    private static string Concat(string a, string b) => a + b;

    public static string Costs(IEntityManager entMan, IPrototypeManager proto, EntityUid? user, FSBuildRecipePrototype recipe)
    {
        var materials = entMan.System<FSCarriedMaterialSystem>();
        var parts = new List<string>();
        foreach (var (material, need) in recipe.Materials)
        {
            var have = user is { } u ? materials.Count(u, material) : 0;
            parts.Add($"[color={(have >= need ? Enough : Short)}]{have}/{need}[/color] {Loc.GetString(proto.Index(material).Name)}");
        }

        return string.Join("  ", parts);
    }

    public static string Caps(FSEngineerOnlyRCDComponent comp, string group)
    {
        var used = comp.CapsUsed.GetValueOrDefault(group);
        var max = comp.Caps.GetValueOrDefault(group);
        return Loc.GetString("fs-rcd-status-caps",
            ("group", Loc.GetString("fs-rcd-cap-group-" + group)), ("used", used), ("max", max),
            ("station", comp.StationUsed), ("stationMax", comp.StationCap));
    }
}
