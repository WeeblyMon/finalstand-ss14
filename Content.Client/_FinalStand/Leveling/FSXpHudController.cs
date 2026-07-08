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

public sealed class FSXpHudController : UIController
{
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private FSLevelingUpdatedEvent? _cached;

    // Controls — non-null only while the game screen is loaded.
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

        // spacerTop pushes barContainer to absolute screen bottom, below the hotbar
        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        LayoutContainer.SetAnchorPreset(_root, LayoutContainer.LayoutPreset.Wide);

        var spacerTop = new Control { VerticalExpand = true, MouseFilter = Control.MouseFilterMode.Ignore };

        // layoutcontainer lets label overlay on top of the bar
        var barContainer = new LayoutContainer
        {
            HorizontalExpand = true,
            SetHeight = 18,
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
        _bar.ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#23707e") };
        _bar.BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#212226") };

        _label = new Label
        {
            Text = "LVL 1",
            Align = Label.AlignMode.Center,
            Modulate = Color.FromHex("#FFFFFF"),
            MouseFilter = Control.MouseFilterMode.Ignore,
            FontOverride = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 12),
        };

        LayoutContainer.SetAnchorPreset(_bar, LayoutContainer.LayoutPreset.Wide);
        LayoutContainer.SetAnchorPreset(_label, LayoutContainer.LayoutPreset.Wide);
        LayoutContainer.SetMarginTop(_label, -8);

        barContainer.AddChild(_bar);
        barContainer.AddChild(_label);

        _root.AddChild(spacerTop);
        _root.AddChild(barContainer);

        // In separated HUD mode, anchor to the viewport container so the bar
        // doesn't extend into the chat panel on the right.
        var isSeparated = Enum.TryParse<ScreenType>(_cfg.GetCVar(CCVars.UILayout), out var st)
                          && st == ScreenType.Separated;
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
