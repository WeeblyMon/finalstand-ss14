using Content.Client._FinalStand.Interface;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._FinalStand.Leveling;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client._FinalStand.Leveling;

public sealed partial class FSXpHudController : UIController
{
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private const int DefaultBarHeight = 16;
    private const int SeparatedBarHeight = 11;
    private const int DefaultFontSize = 12;
    private const int SeparatedFontSize = 9;

    private FSLevelingUpdatedEvent? _cached;

    private BoxContainer? _root;
    private ProgressBar? _bar;
    private Label? _label;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSLevelingUpdatedEvent>(OnLevelingUpdated);

        var screenLoad = UIManager.GetUIController<GameplayStateLoadController>();
        screenLoad.OnScreenLoad += OnScreenLoad;
        screenLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        var screen = UIManager.ActiveScreen;
        if (screen == null) return;

        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        LayoutContainer.SetAnchorPreset(_root, LayoutContainer.LayoutPreset.Wide);

        var isSeparated = Enum.TryParse<ScreenType>(_cfg.GetCVar(CCVars.UILayout), out var st)
                          && st == ScreenType.Separated;

        var spacerTop = new Control { VerticalExpand = true, MouseFilter = Control.MouseFilterMode.Ignore };

        var barContainer = new LayoutContainer
        {
            HorizontalExpand = true,
            SetHeight = isSeparated ? SeparatedBarHeight : DefaultBarHeight,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };

        _bar = new ProgressBar
        {
            HorizontalExpand = true,
            MinValue = 0f,
            MaxValue = 1f,
            Value = 0f,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        // FINALSTAND: palette-matched to the rest of the HUD.
        _bar.ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = FSPalette.Warn };
        _bar.BackgroundStyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = FSPalette.PanelDeep,
            BorderColor = FSPalette.PanelEdge,
            BorderThickness = new Thickness(0, 1, 0, 0),
        };

        _label = new Label
        {
            Text = "LVL 1",
            Align = Label.AlignMode.Center,
            Modulate = FSPalette.TextBright,
            MouseFilter = Control.MouseFilterMode.Ignore,
            FontOverride = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"),
                isSeparated ? SeparatedFontSize : DefaultFontSize),
        };

        LayoutContainer.SetAnchorPreset(_bar, LayoutContainer.LayoutPreset.Wide);
        LayoutContainer.SetAnchorPreset(_label, LayoutContainer.LayoutPreset.Wide);
        LayoutContainer.SetMarginTop(_label, isSeparated ? -5 : -8);

        barContainer.AddChild(_bar);
        barContainer.AddChild(_label);

        _root.AddChild(spacerTop);
        _root.AddChild(barContainer);

        var target = isSeparated ? (FindViewportContainer(screen) ?? (Control) screen) : screen;
        target.AddChild(_root);

        if (_cached != null)
            Apply(_cached);
    }

    private static Control? FindViewportContainer(Control screen)
    {
        foreach (var child in screen.Children)
        {
            if (child is SplitContainer split)
            {
                foreach (var sc in split.Children)
                {
                    if (sc.Name == "ViewportContainer")
                        return sc;
                }
            }
        }
        return null;
    }

    private void OnScreenUnload()
    {
        _root?.Dispose();
        _root = null;
        _bar = null;
        _label = null;
    }

    private void OnLevelingUpdated(FSLevelingUpdatedEvent ev, EntitySessionEventArgs _)
    {
        _cached = ev;
        Apply(ev);
    }

    private void Apply(FSLevelingUpdatedEvent ev)
    {
        if (_bar == null || _label == null) return;

        _bar.Value = ev.XpToNextLevel > 0
            ? (float) ev.Experience / ev.XpToNextLevel
            : 0f;

        _label.Text = ev.PrestigeLevel > 0
            ? $"P{ev.PrestigeLevel}  LVL {ev.Level}  —  {ev.Experience:N0} / {ev.XpToNextLevel:N0} XP"
            : $"LVL {ev.Level}  —  {ev.Experience:N0} / {ev.XpToNextLevel:N0} XP";
    }
}
