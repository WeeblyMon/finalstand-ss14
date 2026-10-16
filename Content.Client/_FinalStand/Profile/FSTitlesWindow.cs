using System.Linq;
using System.Numerics;
using Content.Client._FinalStand.Stylesheets;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared._FinalStand.Titles;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using static Content.Client._FinalStand.Profile.FSProfileWidgets;

namespace Content.Client._FinalStand.Profile;

public sealed class FSTitlesWindow : FancyWindow
{
    private readonly IPrototypeManager _proto;
    private readonly Font _heading;
    private readonly Font _bold;
    private readonly Font _small;
    private readonly RichTextLabel _summary;
    private readonly BoxContainer _list;
    private readonly Button _unequip;

    public event Action<string?>? OnEquip;

    public FSTitlesWindow()
    {
        var resources = IoCManager.Resolve<IResourceCache>();
        ((Control) this).Stylesheet = FSMenuStylesheet.Get(IoCManager.Resolve<IUserInterfaceManager>(), resources);
        _proto = IoCManager.Resolve<IPrototypeManager>();
        _heading = resources.NotoStack("Bold", 16);
        _bold = resources.NotoStack("Bold", 12);
        _small = resources.NotoStack("Regular", 10);

        Title = Loc.GetString("fs-titles-window-title");
        SetSize = new Vector2(560, 560);
        MinSize = new Vector2(460, 360);

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(12, 10),
            SeparationOverride = 8,
        };

        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        var heading = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        heading.AddChild(Text(Loc.GetString("fs-titles-heading"), _heading, FSUiPalette.Currency));
        _summary = Wrapped(string.Empty, FSUiPalette.TextMuted, 400);
        heading.AddChild(_summary);
        top.AddChild(heading);
        _unequip = new Button { Text = Loc.GetString("fs-titles-unequip"), VerticalAlignment = VAlignment.Center };
        _unequip.OnPressed += _ => OnEquip?.Invoke(null);
        top.AddChild(_unequip);
        content.AddChild(top);

        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_list);
        content.AddChild(scroll);

        ContentsContainer.AddChild(content);
    }

    public void Update(FSTitlesStateEvent state)
    {
        _list.RemoveAllChildren();
        var owned = state.Owned.ToHashSet();
        var all = _proto.EnumeratePrototypes<FSTitlePrototype>()
            .OrderByDescending(t => owned.Contains(t.ID))
            .ThenBy(t => Loc.GetString(t.Name), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _summary.SetMessage(Robust.Shared.Utility.FormattedMessage.FromUnformatted(
            Loc.GetString("fs-titles-summary", ("owned", owned.Count), ("total", all.Count))), FSUiPalette.TextMuted);
        _unequip.Disabled = state.Equipped == null;

        foreach (var title in all)
            _list.AddChild(Row(title, owned.Contains(title.ID), state.Equipped == title.ID));
    }

    private Control Row(FSTitlePrototype title, bool owned, bool equipped)
    {
        var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        text.AddChild(Text(Loc.GetString(title.Name), _bold, owned ? title.Color : FSUiPalette.TextMuted));
        text.AddChild(Wrapped(Loc.GetString(title.Description), owned ? FSUiPalette.TextPrimary : FSUiPalette.TextMuted, 360));

        var line = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(10, 8),
        };
        var swatch = new PanelContainer
        {
            MinWidth = 4,
            PanelOverride = new StyleBoxFlat { BackgroundColor = owned ? title.Color : FSUiPalette.BorderNeutral },
        };
        line.AddChild(swatch);
        line.AddChild(text);

        if (equipped)
        {
            line.AddChild(Text(Loc.GetString("fs-titles-equipped"), _bold, FSUiPalette.StatePositive));
        }
        else if (owned)
        {
            var equip = new Button { Text = Loc.GetString("fs-titles-equip"), VerticalAlignment = VAlignment.Center };
            equip.OnPressed += _ => OnEquip?.Invoke(title.ID);
            line.AddChild(equip);
        }
        else
        {
            line.AddChild(Text(Loc.GetString("fs-titles-locked"), _small, FSUiPalette.TextMuted));
        }

        return Card(line,
            equipped ? FSUiPalette.StatePositive : owned ? title.Color.WithAlpha(0.5f) : FSUiPalette.BorderNeutral,
            owned ? FSUiPalette.BgElevated : FSUiPalette.BgRecess);
    }
}
