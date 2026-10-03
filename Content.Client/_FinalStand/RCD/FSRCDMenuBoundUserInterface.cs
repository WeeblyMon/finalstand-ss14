// The RCD picker. Ring layout: one flat radial with a cost panel in the middle that opens with your current piece
// under the cursor. Belt layout: Z steps to the next belt slot instead of opening anything.
using System.Numerics;
using Content.Client.Message;
using Content.Client.UserInterface.Controls;
using Content.Shared._FinalStand.RCD;
using Content.Shared.CCVar;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.RCD;

public sealed class FSRCDMenuBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private const int RingRadius = 120;
    private const int RadiusPerSlice = 5;

    private static readonly Color Unaffordable = Color.FromHex("#2a1d1c").WithAlpha(0.8f);
    private static readonly Color UnaffordableHover = Color.FromHex("#43292a").WithAlpha(0.9f);
    private static readonly Vector2 InfoSize = new(180, 120);
    private static readonly Color Current = Color.FromHex("#d9a441").WithAlpha(0.45f);
    private static readonly Color DeconstructBack = Color.FromHex("#4a1a1a").WithAlpha(0.8f);

    private SimpleRadialMenu? _menu;
    private RichTextLabel? _info;
    private Control? _infoPanel;
    private Control? _toggle;
    private Vector2 _center;

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<RCDComponent>(Owner, out var rcd)
            || !EntMan.TryGetComponent<FSEngineerOnlyRCDComponent>(Owner, out var fs))
            return;

        var palette = new FSRCDPalette(_proto, rcd, fs);
        if (_cfg.GetCVar(CCVars.FSRcdBeltLayout))
        {
            EntMan.System<FSRCDBeltSystem>().Step(Owner, palette, 1);
            // Closing inside Open mutates the open-UI list the engine is iterating, so wait a frame.
            Timer.Spawn(0, Close);
            return;
        }

        var top = new List<RCDPrototype>(palette.Pieces);
        if (palette.Deconstruct != null)
            top.Add(palette.Deconstruct);

        var options = new List<RadialMenuOptionBase>();
        foreach (var piece in top)
            options.Add(Option(piece, fs, rcd));

        if (palette.Station.Count > 0)
        {
            var nested = new List<RadialMenuOptionBase>();
            foreach (var piece in palette.Station)
                nested.Add(Option(piece, fs, rcd));

            options.Add(new RadialMenuNestedLayerOption(nested, RingRadius)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new Robust.Shared.Utility.SpriteSpecifier.Texture(
                    new Robust.Shared.Utility.ResPath("/Textures/Interface/Radial/RCD/walls_and_flooring.png"))),
                ToolTip = Loc.GetString("fs-rcd-ring-station"),
            });
        }

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.SetButtons(options, new SimpleRadialMenuSettings { DefaultContainerRadius = RingRadius });
        _menu.OnClose += RemoveExtras;
        HookHover(top, palette.Station, fs);

        // RadialMenu hides any child it didn't make, so the cost panel and layout toggle live on the popup layer.
        _info = new RichTextLabel { MaxWidth = InfoSize.X - 16, MouseFilter = Control.MouseFilterMode.Ignore };
        _infoPanel = new PanelContainer
        {
            MinWidth = InfoSize.X,
            MouseFilter = Control.MouseFilterMode.Ignore,
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#121010").WithAlpha(0.88f), ContentMarginLeftOverride = 6, ContentMarginTopOverride = 6 },
            Children = { new CenterContainer { MouseFilter = Control.MouseFilterMode.Ignore, Children = { _info } } },
        };
        _toggle = LayoutToggle();
        ShowInfo(_proto.Index(rcd.ProtoId), fs);

        var center = OpenWithCurrentUnderCursor(top, rcd) / _ui.DefaultUIScale;
        _center = center;

        var outer = (RingRadius + (top.Count + 1) * RadiusPerSlice) * 1.5f;
        var below = center.Y + outer + 8;
        var screenHeight = _clyde.ScreenSize.Y / _ui.DefaultUIScale;
        LayoutContainer.SetPosition(_toggle, new Vector2(center.X - 50, below + 30 < screenHeight ? below : center.Y - outer - 38));
        _ui.PopupRoot.AddChild(_infoPanel);
        _ui.PopupRoot.AddChild(_toggle);
        PlaceInfo();
    }

    private void PlaceInfo()
    {
        if (_infoPanel == null)
            return;

        _infoPanel.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        LayoutContainer.SetPosition(_infoPanel, _center - _infoPanel.DesiredSize / 2f);
    }

    private void RemoveExtras()
    {
        _infoPanel?.Orphan();
        _toggle?.Orphan();
        _infoPanel = null;
        _toggle = null;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        RemoveExtras();
    }

    private RadialMenuOptionBase Option(RCDPrototype piece, FSEngineerOnlyRCDComponent fs, RCDComponent rcd)
    {
        var affordable = FSRCDPalette.Affordable(EntMan, _proto, _player.LocalEntity, fs, piece);
        var option = new RadialMenuActionOption<RCDPrototype>(Pick, piece)
        {
            IconSpecifier = RadialMenuIconSpecifier.With(piece.Sprite),
            ToolTip = FSRCDText.Name(_proto, piece),
        };

        if (piece.Mode == RcdMode.Deconstruct)
            option.BackgroundColor = DeconstructBack;
        else if (affordable == 0)
        {
            option.BackgroundColor = Unaffordable;
            option.HoverBackgroundColor = UnaffordableHover;
        }
        else if (piece.ID == rcd.ProtoId.Id)
            option.BackgroundColor = Current;

        return option;
    }

    private Control LayoutToggle()
    {
        var button = new Button { Text = Loc.GetString("fs-rcd-switch-to-belt"), SetWidth = 100 };
        button.OnPressed += _ =>
        {
            _cfg.SetCVar(CCVars.FSRcdBeltLayout, true);
            _cfg.SaveToFile();
            Close();
        };
        return button;
    }

    // SimpleRadialMenu lays buttons out in model order, so the n-th button of a layer is the n-th piece we gave it.
    private void HookHover(List<RCDPrototype> top, List<RCDPrototype> station, FSEngineerOnlyRCDComponent fs)
    {
        if (_menu == null)
            return;

        var layers = new List<RadialContainer>();
        foreach (var child in _menu.Children)
        {
            if (child is RadialContainer container)
                layers.Add(container);
        }

        for (var i = 0; i < layers.Count; i++)
        {
            var pieces = i == 0 ? top : station;
            var index = 0;
            foreach (var child in layers[i].Children)
            {
                if (child is not RadialMenuButton button || index >= pieces.Count)
                    continue;

                var piece = pieces[index++];
                button.OnMouseEntered += _ => ShowInfo(piece, fs);
            }
        }
    }

    private void ShowInfo(RCDPrototype piece, FSEngineerOnlyRCDComponent fs)
    {
        _info?.SetMarkup(FSRCDPalette.Info(EntMan, _proto, _player.LocalEntity, fs, piece));
        PlaceInfo();
    }

    private Vector2 OpenWithCurrentUnderCursor(List<RCDPrototype> top, RCDComponent rcd)
    {
        var mouse = _input.MouseScreenPosition.Position;
        var index = top.FindIndex(p => p.ID == rcd.ProtoId.Id);
        if (index >= 0)
        {
            var count = top.Count + 1;
            var radius = RingRadius + count * RadiusPerSlice;
            var angle = MathF.Tau / count * (index + 0.5f) + MathF.PI * 0.5f;
            var offset = new Vector2(radius * MathF.Cos(angle), -radius * MathF.Sin(angle)) * _ui.DefaultUIScale;
            mouse -= offset;
        }

        _menu!.OpenCenteredAt(mouse / _clyde.ScreenSize);
        return mouse;
    }

    private void Pick(RCDPrototype piece)
    {
        SendMessage(new RCDSystemMessage(piece.ID));
    }
}
