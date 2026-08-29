using System.Numerics;
using Content.Client._FinalStand.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared._FinalStand.Leaderboard;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.Client._FinalStand.Leaderboard;

public sealed class FSLeaderboardWindow : FancyWindow
{
    private readonly BoxContainer _rows;
    private readonly List<Row> _pool = new();

    public FSLeaderboardWindow()
    {
        ((Control)this).Stylesheet = FSMenuStylesheet.Get(
            IoCManager.Resolve<IUserInterfaceManager>(),
            IoCManager.Resolve<IResourceCache>());

        Title = Loc.GetString("final-stand-leaderboard-title");
        SetSize = new Vector2(980, 620);
        MinSize = new Vector2(760, 420);
        Resizable = true;

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(8),
        };

        var headerGrid = new GridContainer
        {
            Columns = 7,
            HorizontalExpand = true,
            Margin = new Thickness(10, 8),
        };

        headerGrid.AddChild(Cell("final-stand-leaderboard-rank", 42, Label.AlignMode.Center, FSUiPalette.TextMuted));
        headerGrid.AddChild(Cell("final-stand-leaderboard-name", 220, Label.AlignMode.Left, FSUiPalette.TextMuted, expand: true));
        headerGrid.AddChild(Cell("final-stand-leaderboard-level", 90, Label.AlignMode.Center, FSUiPalette.TextMuted));
        headerGrid.AddChild(Cell("final-stand-leaderboard-kills", 80, Label.AlignMode.Center, FSUiPalette.TextMuted));
        headerGrid.AddChild(Cell("final-stand-leaderboard-assists", 90, Label.AlignMode.Center, FSUiPalette.TextMuted));
        headerGrid.AddChild(Cell("final-stand-leaderboard-credits", 110, Label.AlignMode.Center, FSUiPalette.TextMuted));
        headerGrid.AddChild(Cell("final-stand-leaderboard-score", 120, Label.AlignMode.Right, FSUiPalette.TextMuted));

        var header = new PanelContainer
        {
            MinHeight = 40,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 6),
        };
        header.AddChild(headerGrid);
        content.AddChild(header);

        _rows = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 4,
        };

        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        scroll.AddChild(_rows);
        content.AddChild(scroll);

        ContentsContainer.AddChild(content);
    }

    /// <summary>
    /// Entries arrive already sorted from the server. Rows are reused and hidden rather than rebuilt,
    /// since this runs on every snapshot while the window is open.
    /// </summary>
    public void Populate(FSLeaderboardEntry[] entries)
    {
        while (_pool.Count < entries.Length)
        {
            var row = new Row();
            _pool.Add(row);
            _rows.AddChild(row.Panel);
        }

        for (var i = 0; i < _pool.Count; i++)
        {
            var row = _pool[i];
            row.Panel.Visible = i < entries.Length;

            if (i < entries.Length)
                row.Set(i + 1, entries[i]);
        }
    }

    private static Label Cell(string locId, float minWidth, Label.AlignMode align, Color color, bool expand = false)
    {
        return new Label
        {
            Text = Loc.GetString(locId),
            MinWidth = minWidth,
            Align = align,
            FontColorOverride = color,
            HorizontalExpand = expand,
        };
    }

    private sealed class Row
    {
        public readonly PanelContainer Panel;

        private readonly Label _rank;
        private readonly Label _name;
        private readonly Label _level;
        private readonly Label _kills;
        private readonly Label _assists;
        private readonly Label _credits;
        private readonly Label _score;

        public Row()
        {
            _rank = Make(42, Label.AlignMode.Center, FSUiPalette.TextPrimary);
            _name = Make(220, Label.AlignMode.Left, FSUiPalette.TextPrimary, expand: true, clip: true);
            _level = Make(90, Label.AlignMode.Center, FSUiPalette.TextPrimary);
            _kills = Make(80, Label.AlignMode.Center, FSUiPalette.TextPrimary);
            _assists = Make(90, Label.AlignMode.Center, FSUiPalette.TextPrimary);
            _credits = Make(110, Label.AlignMode.Center, FSUiPalette.Currency);
            _score = Make(120, Label.AlignMode.Right, FSUiPalette.AccentBrand);

            var grid = new GridContainer
            {
                Columns = 7,
                HorizontalExpand = true,
                Margin = new Thickness(10, 5),
            };

            grid.AddChild(_rank);
            grid.AddChild(_name);
            grid.AddChild(_level);
            grid.AddChild(_kills);
            grid.AddChild(_assists);
            grid.AddChild(_credits);
            grid.AddChild(_score);

            Panel = new PanelContainer
            {
                HorizontalExpand = true,
                MinHeight = 34,
            };
            Panel.AddChild(grid);
        }

        public void Set(int rank, FSLeaderboardEntry entry)
        {
            _rank.Text = rank.ToString();
            _name.Text = entry.Name;
            _level.Text = entry.Level.ToString();
            _kills.Text = entry.Kills.ToString();
            _assists.Text = entry.Assists.ToString();
            _credits.Text = entry.Credits.ToString("N0");
            _score.Text = entry.Score.ToString("N0");
        }

        private static Label Make(float minWidth, Label.AlignMode align, Color color, bool expand = false, bool clip = false)
        {
            return new Label
            {
                MinWidth = minWidth,
                Align = align,
                FontColorOverride = color,
                HorizontalExpand = expand,
                ClipText = clip,
            };
        }
    }
}
