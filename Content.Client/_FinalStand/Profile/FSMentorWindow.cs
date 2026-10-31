using System.Numerics;
using Content.Client._FinalStand.Stylesheets;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared._FinalStand.Mentor;
using Content.Shared._FinalStand.Titles;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using static Content.Client._FinalStand.Profile.FSProfileWidgets;

namespace Content.Client._FinalStand.Profile;

public sealed class FSMentorWindow : FancyWindow
{
    private static readonly (LocId Text, int Points)[] Earning =
    {
        ("fs-mentor-earn-revive", FSMentorRules.ReviveMp),
        ("fs-mentor-earn-heal", 1),
        ("fs-mentor-earn-escort", FSMentorRules.WaveEscortMp),
        ("fs-mentor-earn-first-round", FSMentorRules.FirstRoundMp),
    };

    private readonly IPrototypeManager _proto;
    private readonly Font _heading;
    private readonly Font _big;
    private readonly Font _bold;
    private readonly Font _small;
    private readonly BoxContainer _body;

    public event Action<string>? OnBuy;

    public FSMentorWindow()
    {
        var resources = IoCManager.Resolve<IResourceCache>();
        ((Control) this).Stylesheet = FSMenuStylesheet.Get(IoCManager.Resolve<IUserInterfaceManager>(), resources);
        _proto = IoCManager.Resolve<IPrototypeManager>();
        _heading = resources.NotoStack("Bold", 16);
        _big = resources.NotoStack("Bold", 24);
        _bold = resources.NotoStack("Bold", 12);
        _small = resources.NotoStack("Regular", 10);

        Title = Loc.GetString("fs-mentor-window-title");
        SetSize = new Vector2(600, 620);
        MinSize = new Vector2(480, 420);

        _body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(12, 10),
            SeparationOverride = 10,
        };
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_body);
        ContentsContainer.AddChild(scroll);
        _body.AddChild(Text(Loc.GetString("fs-mentor-window-loading"), _small, FSUiPalette.TextMuted));
    }

    public void Update(FSMentorStateEvent state)
    {
        _body.RemoveAllChildren();
        _body.AddChild(Text(Loc.GetString("fs-mentor-heading"), _heading, FSUiPalette.Currency));
        _body.AddChild(Wrapped(Loc.GetString("fs-mentor-intro"), FSUiPalette.TextMuted, 540));

        var stats = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        stats.AddChild(Stat(Loc.GetString("fs-mentor-stat-balance"), state.Balance.ToString("N0"), FSUiPalette.Currency));
        stats.AddChild(Stat(Loc.GetString("fs-mentor-stat-total"), state.Total.ToString("N0"), FSUiPalette.TextPrimary));
        stats.AddChild(Stat(Loc.GetString("fs-mentor-stat-week"),
            Loc.GetString("fs-mentor-week-value", ("points", state.WeekPoints), ("cap", state.WeekCap)), FSUiPalette.StatePending));
        _body.AddChild(stats);

        _body.AddChild(Eligibility(state));
        _body.AddChild(HowToEarn());
        _body.AddChild(Shop(state));

        if (state.Message != null)
            _body.AddChild(Text(state.Message, _bold, FSUiPalette.StatePositive));
    }

    private Control Stat(string label, string value, Color color)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(10, 8) };
        box.AddChild(Text(label.ToUpperInvariant(), _small, FSUiPalette.TextMuted));
        box.AddChild(Text(value, _big, color));
        return Card(box, color.WithAlpha(0.4f));
    }

    private Control Eligibility(FSMentorStateEvent state)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(10, 8),
        };
        box.AddChild(Text(Loc.GetString(state.Eligible ? "fs-mentor-eligible" : "fs-mentor-not-eligible"), _bold,
            state.Eligible ? FSUiPalette.StatePositive : FSUiPalette.StatePending));

        box.AddChild(Requirement(Loc.GetString("fs-mentor-req-hours",
                ("hours", Math.Floor(state.Hours)), ("needed", state.MinHours)),
            (float) (state.Hours / Math.Max(1, state.MinHours))));
        box.AddChild(Requirement(Loc.GetString("fs-mentor-req-level", ("level", state.Level), ("needed", state.MinLevel)),
            state.Level / (float) Math.Max(1, state.MinLevel)));

        return Card(box, state.Eligible ? FSUiPalette.StatePositive.WithAlpha(0.5f) : FSUiPalette.BorderNeutral);
    }

    private Control Requirement(string text, float progress)
    {
        var done = progress >= 1f;
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        row.AddChild(Text((done ? "✓ " : "  ") + text, _small, done ? FSUiPalette.StatePositive : FSUiPalette.TextPrimary));
        row.AddChild(Bar(progress, done ? FSUiPalette.StatePositive : FSUiPalette.StatePending));
        return row;
    }

    private Control HowToEarn()
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(10, 8),
        };
        box.AddChild(Text(Loc.GetString("fs-mentor-earn-heading"), _bold, FSUiPalette.TextPrimary));
        foreach (var (text, points) in Earning)
        {
            var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
            line.AddChild(Text(Loc.GetString("fs-mentor-earn-points", ("points", points)), _bold, FSUiPalette.Currency));
            line.AddChild(Text(Loc.GetString(text, ("health", (int) FSMentorRules.HealPerMp)), _small, FSUiPalette.TextPrimary, expand: true));
            box.AddChild(line);
        }
        box.AddChild(Wrapped(Loc.GetString("fs-mentor-earn-rules"), FSUiPalette.TextMuted, 520));
        return Card(box, FSUiPalette.BorderNeutral);
    }

    private Control Shop(FSMentorStateEvent state)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(10, 8),
        };
        box.AddChild(Text(Loc.GetString("fs-mentor-shop-heading"), _bold, FSUiPalette.TextPrimary));

        foreach (var item in state.Shop)
        {
            var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
            if (item.Title == null)
            {
                text.AddChild(Text(Loc.GetString("fs-mentor-shop-perk-points", ("points", item.PerkPoints)), _bold, FSUiPalette.Currency));
                text.AddChild(Text(Loc.GetString("fs-mentor-shop-perk-desc"), _small, FSUiPalette.TextMuted));
            }
            else
            {
                var titleId = item.Title.Value;
                var proto = _proto.TryIndex(titleId, out var title) ? title : null;
                text.AddChild(Text(Loc.GetString("fs-mentor-shop-title", ("title", proto != null ? Loc.GetString(proto.Name) : titleId.Id)),
                    _bold, proto?.Color ?? FSUiPalette.Currency));
                if (proto != null)
                    text.AddChild(Text(Loc.GetString(proto.Description), _small, FSUiPalette.TextMuted));
                if (proto?.BuffDescription is { } buffText)
                    text.AddChild(Text(Loc.GetString("fs-titles-buff-line", ("buff", Loc.GetString(buffText))), _small, FSUiPalette.StatePositive));
            }

            var line = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 8,
                Margin = new Thickness(8, 6),
            };
            line.AddChild(text);

            if (item.Owned)
            {
                line.AddChild(Text(Loc.GetString("fs-mentor-shop-owned-label"), _bold, FSUiPalette.StatePositive));
            }
            else
            {
                var buy = new Button
                {
                    Text = Loc.GetString("fs-mentor-shop-buy", ("cost", item.Cost)),
                    Disabled = state.Balance < item.Cost,
                    VerticalAlignment = VAlignment.Center,
                };
                var id = item.Id;
                buy.OnPressed += _ => OnBuy?.Invoke(id);
                line.AddChild(buy);
            }

            box.AddChild(Card(line, FSUiPalette.BorderNeutral, FSUiPalette.BgRecess));
        }

        return Card(box, FSUiPalette.Currency.WithAlpha(0.4f));
    }
}
