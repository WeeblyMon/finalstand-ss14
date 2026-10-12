using System.Numerics;
using Content.Client._FinalStand.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._FinalStand.Ladder;

public sealed class FSLadderWindow : FancyWindow
{
    private readonly BoxContainer _rows;
    private readonly Label _empty;
    private readonly Dictionary<FSDifficulty, Button> _difficultyButtons = new();
    private readonly Dictionary<FSPlayerBracket, Button> _bracketButtons = new();

    public FSDifficulty Difficulty { get; private set; } = FSDifficulty.Normal;
    public FSPlayerBracket Bracket { get; private set; } = FSPlayerBracket.Crew;
    public bool Weekly { get; private set; }

    private readonly Button _weeklyButton;
    private readonly Label _weeklyName;

    public event Action<FSDifficulty, FSPlayerBracket, bool>? OnSelectionChanged;

    public FSLadderWindow()
    {
        ((Control) this).Stylesheet = FSMenuStylesheet.Get(
            IoCManager.Resolve<IUserInterfaceManager>(),
            IoCManager.Resolve<IResourceCache>());

        Title = Loc.GetString("fs-ladder-window-title");
        SetSize = new Vector2(860, 560);
        MinSize = new Vector2(640, 380);
        Resizable = true;

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(10),
            SeparationOverride = 8,
        };

        var filters = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        foreach (var difficulty in Enum.GetValues<FSDifficulty>())
        {
            var button = Toggle(Loc.GetString(FSDifficultyRules.NameLocId(difficulty)));
            button.OnPressed += _ => Select(difficulty, Bracket, false);
            _difficultyButtons[difficulty] = button;
            filters.AddChild(button);
        }

        _weeklyButton = Toggle(Loc.GetString("fs-ladder-weekly"));
        _weeklyButton.OnPressed += _ => Select(Difficulty, Bracket, true);
        filters.AddChild(_weeklyButton);

        filters.AddChild(new Control { HorizontalExpand = true });

        foreach (var bracket in Enum.GetValues<FSPlayerBracket>())
        {
            var button = Toggle(Loc.GetString(FSLadder.BracketLocId(bracket)));
            button.OnPressed += _ => Select(Difficulty, bracket, false);
            _bracketButtons[bracket] = button;
            filters.AddChild(button);
        }

        content.AddChild(filters);

        _weeklyName = new Label { FontColorOverride = FSUiPalette.Currency, Visible = false, Margin = new Thickness(8, 0) };
        content.AddChild(_weeklyName);

        var header = new GridContainer { Columns = 6, HorizontalExpand = true, Margin = new Thickness(8, 6) };
        header.AddChild(Cell(Loc.GetString("fs-ladder-col-rank"), 36, FSUiPalette.TextMuted));
        header.AddChild(Cell(Loc.GetString("fs-ladder-col-result"), 100, FSUiPalette.TextMuted));
        header.AddChild(Cell(Loc.GetString("fs-ladder-col-time"), 80, FSUiPalette.TextMuted));
        header.AddChild(Cell(Loc.GetString("fs-ladder-col-kills"), 70, FSUiPalette.TextMuted));
        header.AddChild(Cell(Loc.GetString("fs-ladder-col-date"), 100, FSUiPalette.TextMuted));
        header.AddChild(Cell(Loc.GetString("fs-ladder-col-players"), 200, FSUiPalette.TextMuted, expand: true));
        content.AddChild(header);

        _rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        _empty = new Label
        {
            Text = Loc.GetString("fs-ladder-window-empty"),
            FontColorOverride = FSUiPalette.TextMuted,
            Margin = new Thickness(8),
            Visible = false,
        };

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        var list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        list.AddChild(_empty);
        list.AddChild(_rows);
        scroll.AddChild(list);
        content.AddChild(scroll);

        ContentsContainer.AddChild(content);
        RefreshToggles();
    }

    public void Select(FSDifficulty difficulty, FSPlayerBracket bracket, bool weekly)
    {
        Difficulty = difficulty;
        Bracket = bracket;
        Weekly = weekly;
        RefreshToggles();
        ShowLoading();
        OnSelectionChanged?.Invoke(difficulty, bracket, weekly);
    }

    public void ShowLoading()
    {
        _rows.RemoveAllChildren();
        _weeklyName.Visible = false;
        _empty.Text = Loc.GetString("fs-ladder-window-loading");
        _empty.Visible = true;
    }

    public void Populate(FSLadderResponseEvent response)
    {
        if (response.Weekly != Weekly || !Weekly && (response.Difficulty != Difficulty || response.Bracket != Bracket))
            return;

        _weeklyName.Visible = Weekly && response.WeeklyName != null;
        if (response.WeeklyName != null)
            _weeklyName.Text = Loc.GetString("fs-ladder-weekly-header", ("name", response.WeeklyName));

        _rows.RemoveAllChildren();
        _empty.Text = Loc.GetString("fs-ladder-window-empty");
        _empty.Visible = response.Runs.Length == 0;

        for (var i = 0; i < response.Runs.Length; i++)
        {
            var run = response.Runs[i];
            var color = i == 0 ? FSUiPalette.Currency : FSUiPalette.TextPrimary;
            var grid = new GridContainer { Columns = 6, HorizontalExpand = true, Margin = new Thickness(8, 4) };
            grid.AddChild(Cell((i + 1).ToString(), 36, color));
            grid.AddChild(Cell(run.Victory
                    ? Loc.GetString("fs-ladder-result-victory")
                    : Loc.GetString("fs-ladder-result-wave", ("waves", run.Waves)),
                100, run.Victory ? FSUiPalette.StatePositive : color));
            grid.AddChild(Cell(TimeSpan.FromSeconds(run.DurationSeconds).ToString(@"h\:mm\:ss"), 80, color));
            grid.AddChild(Cell(run.Kills.ToString("N0"), 70, color));
            grid.AddChild(Cell(run.EndedAtUtc.ToLocalTime().ToString("yyyy-MM-dd"), 100, FSUiPalette.TextMuted));
            grid.AddChild(Cell(run.Players, 200, color, expand: true, clip: true));

            var panel = new PanelContainer { HorizontalExpand = true, MinHeight = 30 };
            panel.AddChild(grid);
            _rows.AddChild(panel);
        }
    }

    private void RefreshToggles()
    {
        foreach (var (difficulty, button) in _difficultyButtons)
            button.Pressed = !Weekly && difficulty == Difficulty;
        foreach (var (bracket, button) in _bracketButtons)
        {
            button.Pressed = !Weekly && bracket == Bracket;
            button.Disabled = Weekly;
        }
        _weeklyButton.Pressed = Weekly;
    }

    private static Button Toggle(string text) => new() { Text = text, ToggleMode = true, MinHeight = 28 };

    private static Label Cell(string text, float minWidth, Color color, bool expand = false, bool clip = false) => new()
    {
        Text = text,
        MinWidth = minWidth,
        FontColorOverride = color,
        HorizontalExpand = expand,
        ClipText = clip,
    };
}
