using System.Numerics;
using Content.Client._FinalStand.Ladder;
using Content.Client._FinalStand.Stylesheets;
using Content.Client._FinalStand.WaveHud;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared._FinalStand.Leaderboard;
using Content.Shared._FinalStand.Titles;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Leaderboard;

public sealed class FSLeaderboardWindow : FancyWindow
{
    private static readonly Color[] Medals =
    {
        Color.FromHex("#e3b448"),
        Color.FromHex("#c9ccd1"),
        Color.FromHex("#c27c48"),
    };

    private const int PodiumSize = 3;
    private const float RankWidth = 44f;
    private const float StatWidth = 76f;
    private const float ScoreWidth = 150f;

    private readonly Font _heading;
    private readonly Font _big;
    private readonly Font _bold;
    private readonly Font _small;

    private readonly Label _summary;
    private readonly BoxContainer _podium;
    private readonly PodiumCard[] _cards = new PodiumCard[PodiumSize];
    private readonly BoxContainer _rows;
    private readonly Label _empty;
    private readonly List<Row> _pool = new();

    public FSLeaderboardWindow()
    {
        var resources = IoCManager.Resolve<IResourceCache>();
        ((Control) this).Stylesheet = FSMenuStylesheet.Get(IoCManager.Resolve<IUserInterfaceManager>(), resources);

        _heading = resources.NotoStack("Bold", 18);
        _big = resources.NotoStack("Bold", 22);
        _bold = resources.NotoStack("Bold", 12);
        _small = resources.NotoStack("Regular", 10);

        Title = Loc.GetString("final-stand-leaderboard-title");
        SetSize = new Vector2(1000, 660);
        MinSize = new Vector2(820, 460);
        Resizable = true;

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(12, 10),
            SeparationOverride = 10,
        };

        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        var titles = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        titles.AddChild(new Label
        {
            Text = Loc.GetString("fs-leaderboard-heading"),
            FontOverride = _heading,
            FontColorOverride = FSUiPalette.Currency,
        });
        _summary = new Label { FontOverride = _small, FontColorOverride = FSUiPalette.TextMuted };
        titles.AddChild(_summary);
        top.AddChild(titles);

        var records = new Button
        {
            Text = Loc.GetString("fs-ladder-open-records"),
            MinHeight = 32,
            VerticalAlignment = VAlignment.Center,
        };
        records.OnPressed += _ => IoCManager.Resolve<IUserInterfaceManager>().GetUIController<FSLadderUIController>().Open();
        top.AddChild(records);
        content.AddChild(top);
        content.AddChild(Rule(FSUiPalette.Currency.WithAlpha(0.45f)));

        _podium = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        for (var i = 0; i < PodiumSize; i++)
        {
            _cards[i] = new PodiumCard(i, this);
            _podium.AddChild(_cards[i].Panel);
        }
        content.AddChild(_podium);

        content.AddChild(BuildHeader());

        _rows = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 3,
        };
        _empty = new Label
        {
            Text = Loc.GetString("fs-leaderboard-empty"),
            FontColorOverride = FSUiPalette.TextMuted,
            Align = Label.AlignMode.Center,
            Margin = new Thickness(0, 24),
        };

        var list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        list.AddChild(_empty);
        list.AddChild(_rows);
        var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(list);
        content.AddChild(scroll);

        ContentsContainer.AddChild(content);
        Populate(Array.Empty<FSLeaderboardEntry>());
    }

    public void Populate(FSLeaderboardEntry[] entries)
    {
        var local = LocalName();
        var topScore = entries.Length > 0 ? Math.Max(1, entries[0].Score) : 1;

        _summary.Text = Summary(entries.Length);
        _empty.Visible = entries.Length == 0;
        _podium.Visible = entries.Length > 0;

        for (var i = 0; i < PodiumSize; i++)
            _cards[i].Set(i < entries.Length ? entries[i] : null, local);

        while (_pool.Count < entries.Length)
        {
            var row = new Row(this);
            _pool.Add(row);
            _rows.AddChild(row.Panel);
        }

        for (var i = 0; i < _pool.Count; i++)
        {
            _pool[i].Panel.Visible = i < entries.Length;
            if (i < entries.Length)
                _pool[i].Set(i + 1, entries[i], topScore, entries[i].Name == local);
        }
    }

    private static string Summary(int players)
    {
        var overlays = IoCManager.Resolve<IOverlayManager>();
        return overlays.TryGetOverlay<WaveHudOverlay>(out var hud) && hud.CurrentWave > 0
            ? Loc.GetString("fs-leaderboard-summary-wave", ("wave", hud.CurrentWave), ("players", players))
            : Loc.GetString("fs-leaderboard-summary", ("players", players));
    }

    private static string? LocalName()
    {
        var entities = IoCManager.Resolve<IEntityManager>();
        return IoCManager.Resolve<IPlayerManager>().LocalEntity is { } local && entities.TryGetComponent(local, out MetaDataComponent? meta)
            ? meta.EntityName
            : null;
    }

    private static Color Medal(int rank) => rank <= Medals.Length ? Medals[rank - 1] : FSUiPalette.TextMuted;

    private static (string? Text, Color Color) TitleOf(FSLeaderboardEntry entry)
    {
        if (entry.Title == null || !IoCManager.Resolve<IPrototypeManager>().TryIndex<FSTitlePrototype>(entry.Title, out var title))
            return (null, FSUiPalette.TextMuted);
        return (Loc.GetString(title.Name), title.Color);
    }

    private static Control Rule(Color color)
    {
        return new PanelContainer
        {
            MinHeight = 1,
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat { BackgroundColor = color },
        };
    }

    private static StyleBoxFlat Box(Color back, Color border, float thickness = 1f)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = back,
            BorderColor = border,
            BorderThickness = new Thickness(thickness),
        };
    }

    private Control BuildHeader()
    {
        var grid = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(10, 6) };
        grid.AddChild(HeaderCell("final-stand-leaderboard-rank", RankWidth, Label.AlignMode.Center));
        grid.AddChild(HeaderCell("final-stand-leaderboard-name", 0, Label.AlignMode.Left, expand: true));
        grid.AddChild(HeaderCell("final-stand-leaderboard-level", StatWidth, Label.AlignMode.Center));
        grid.AddChild(HeaderCell("final-stand-leaderboard-kills", StatWidth, Label.AlignMode.Center));
        grid.AddChild(HeaderCell("final-stand-leaderboard-assists", StatWidth, Label.AlignMode.Center));
        grid.AddChild(HeaderCell("final-stand-leaderboard-healing", StatWidth, Label.AlignMode.Center));
        grid.AddChild(HeaderCell("final-stand-leaderboard-fortify", StatWidth, Label.AlignMode.Center));
        grid.AddChild(HeaderCell("final-stand-leaderboard-credits", StatWidth + 10, Label.AlignMode.Center));
        grid.AddChild(HeaderCell("final-stand-leaderboard-score", ScoreWidth, Label.AlignMode.Right));

        var panel = new PanelContainer { PanelOverride = Box(FSUiPalette.BgRecess, FSUiPalette.BorderNeutral) };
        panel.AddChild(grid);
        return panel;
    }

    private Label HeaderCell(string locId, float width, Label.AlignMode align, bool expand = false)
    {
        return new Label
        {
            Text = Loc.GetString(locId).ToUpperInvariant(),
            FontOverride = _small,
            FontColorOverride = FSUiPalette.TextMuted,
            MinWidth = width,
            Align = align,
            HorizontalExpand = expand,
        };
    }

    private sealed class PodiumCard
    {
        public readonly PanelContainer Panel;

        private readonly int _place;
        private readonly Label _name;
        private readonly Label _title;
        private readonly Label _score;
        private readonly Label _stats;
        private readonly StyleBoxFlat _style;

        public PodiumCard(int place, FSLeaderboardWindow window)
        {
            _place = place;
            var medal = Medals[place];
            _style = Box(FSUiPalette.BgSurface, medal.WithAlpha(0.55f));

            var body = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Margin = new Thickness(12, 8),
                SeparationOverride = 2,
            };

            var head = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
            head.AddChild(new Label
            {
                Text = Loc.GetString("fs-leaderboard-place", ("place", place + 1)),
                FontOverride = window._bold,
                FontColorOverride = medal,
            });
            _name = new Label { FontOverride = window._bold, FontColorOverride = FSUiPalette.TextPrimary, ClipText = true, HorizontalExpand = true };
            head.AddChild(_name);
            body.AddChild(head);

            _title = new Label { FontOverride = window._small, ClipText = true };
            body.AddChild(_title);

            _score = new Label { FontOverride = window._big, FontColorOverride = medal };
            body.AddChild(_score);

            _stats = new Label { FontOverride = window._small, FontColorOverride = FSUiPalette.TextMuted, ClipText = true };
            body.AddChild(_stats);

            var stripe = new PanelContainer { MinHeight = 3, PanelOverride = new StyleBoxFlat { BackgroundColor = medal } };
            var stack = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
            stack.AddChild(stripe);
            stack.AddChild(body);

            Panel = new PanelContainer { HorizontalExpand = true, PanelOverride = _style, MinHeight = 108 };
            Panel.AddChild(stack);
        }

        public void Set(FSLeaderboardEntry? entry, string? local)
        {
            if (entry == null)
            {
                _name.Text = Loc.GetString("fs-leaderboard-podium-empty");
                _name.FontColorOverride = FSUiPalette.TextMuted;
                _title.Text = string.Empty;
                _score.Text = "-";
                _stats.Text = string.Empty;
                _style.BackgroundColor = FSUiPalette.BgRecess;
                return;
            }

            _name.Text = entry.Name;
            _name.FontColorOverride = entry.Name == local ? FSUiPalette.Currency : FSUiPalette.TextPrimary;
            var (title, color) = TitleOf(entry);
            _title.Text = entry.Rookie
                ? Loc.GetString("fs-badge-leaderboard-rookie") + (title != null ? "  " + title : string.Empty)
                : title ?? Loc.GetString("fs-leaderboard-level", ("level", entry.Level));
            _title.FontColorOverride = entry.Rookie ? FSUiPalette.StatePositive : color;
            _score.Text = Loc.GetString("fs-leaderboard-points", ("score", entry.Score.ToString("N0")));
            _stats.Text = Loc.GetString("fs-leaderboard-podium-stats",
                ("kills", entry.Kills), ("assists", entry.Assists), ("healing", entry.Healing.ToString("N0")));
            _style.BackgroundColor = _place == 0 ? FSUiPalette.BgElevated : FSUiPalette.BgSurface;
        }
    }

    private sealed class Row
    {
        public readonly PanelContainer Panel;

        private readonly StyleBoxFlat _style;
        private readonly Label _rank;
        private readonly Label _name;
        private readonly Label _title;
        private readonly Label _rookie;
        private readonly Label _level;
        private readonly Label _kills;
        private readonly Label _assists;
        private readonly Label _healing;
        private readonly Label _fortify;
        private readonly Label _credits;
        private readonly Label _score;
        private readonly ProgressBar _bar;

        public Row(FSLeaderboardWindow window)
        {
            _rank = Make(window._bold, RankWidth, Label.AlignMode.Center, FSUiPalette.TextPrimary);
            _name = Make(window._bold, 0, Label.AlignMode.Left, FSUiPalette.TextPrimary);
            _title = Make(window._small, 0, Label.AlignMode.Left, FSUiPalette.Currency);
            _rookie = Make(window._small, 0, Label.AlignMode.Left, FSUiPalette.StatePositive);
            _rookie.Text = Loc.GetString("fs-badge-leaderboard-rookie");

            var nameCell = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                SeparationOverride = 8,
            };
            nameCell.AddChild(_name);
            nameCell.AddChild(_title);
            nameCell.AddChild(_rookie);

            _level = Make(null, StatWidth, Label.AlignMode.Center, FSUiPalette.TextPrimary);
            _kills = Make(null, StatWidth, Label.AlignMode.Center, FSUiPalette.TextPrimary);
            _assists = Make(null, StatWidth, Label.AlignMode.Center, FSUiPalette.TextPrimary);
            _healing = Make(null, StatWidth, Label.AlignMode.Center, FSUiPalette.StatePositive);
            _fortify = Make(null, StatWidth, Label.AlignMode.Center, FSUiPalette.StatePending);
            _credits = Make(null, StatWidth + 10, Label.AlignMode.Center, FSUiPalette.Currency);
            _score = Make(window._bold, ScoreWidth, Label.AlignMode.Right, FSUiPalette.TextPrimary);

            _bar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 1,
                MinHeight = 3,
                MaxHeight = 3,
                MinWidth = ScoreWidth,
                HorizontalAlignment = HAlignment.Right,
                BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = FSUiPalette.BgTrack },
                ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = FSUiPalette.Currency },
            };
            var scoreCell = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = ScoreWidth };
            scoreCell.AddChild(_score);
            scoreCell.AddChild(_bar);

            var line = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new Thickness(10, 6),
            };
            line.AddChild(_rank);
            line.AddChild(nameCell);
            line.AddChild(_level);
            line.AddChild(_kills);
            line.AddChild(_assists);
            line.AddChild(_healing);
            line.AddChild(_fortify);
            line.AddChild(_credits);
            line.AddChild(scoreCell);

            _style = Box(FSUiPalette.BgSurface, FSUiPalette.BorderNeutral);
            Panel = new PanelContainer { HorizontalExpand = true, MinHeight = 38, PanelOverride = _style };
            Panel.AddChild(line);
        }

        public void Set(int rank, FSLeaderboardEntry entry, int topScore, bool isLocal)
        {
            _rank.Text = Loc.GetString("fs-leaderboard-place", ("place", rank));
            _rank.FontColorOverride = Medal(rank);
            _name.Text = entry.Name;
            _name.FontColorOverride = isLocal ? FSUiPalette.Currency : FSUiPalette.TextPrimary;
            _rookie.Visible = entry.Rookie;

            var (title, color) = TitleOf(entry);
            _title.Visible = title != null;
            _title.Text = title ?? string.Empty;
            _title.FontColorOverride = color;

            _level.Text = entry.Level.ToString();
            _kills.Text = entry.Kills.ToString("N0");
            _assists.Text = entry.Assists.ToString("N0");
            Stat(_healing, entry.Healing, FSUiPalette.StatePositive);
            Stat(_fortify, entry.Fortify, FSUiPalette.StatePending);
            Stat(_credits, entry.Credits, FSUiPalette.Currency);
            _score.Text = entry.Score.ToString("N0");
            _bar.Value = Math.Clamp(entry.Score / (float) topScore, 0f, 1f);

            _style.BackgroundColor = rank % 2 == 0 ? FSUiPalette.BgSurface : FSUiPalette.BgElevated;
            _style.BorderColor = isLocal ? FSUiPalette.Currency : FSUiPalette.BorderNeutral;
        }

        private static void Stat(Label label, int value, Color color)
        {
            label.Text = value.ToString("N0");
            label.FontColorOverride = value > 0 ? color : FSUiPalette.TextMuted;
        }

        private static Label Make(Font? font, float width, Label.AlignMode align, Color color)
        {
            return new Label
            {
                FontOverride = font,
                MinWidth = width,
                Align = align,
                FontColorOverride = color,
                VerticalAlignment = VAlignment.Center,
            };
        }
    }
}
