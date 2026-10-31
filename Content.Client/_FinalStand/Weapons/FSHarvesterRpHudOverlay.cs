using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Weapons;

// Mirrors FSBattleTranceHudOverlay - a screen-space overlay above the hand HUD, not an ItemStatus panel.
public sealed partial class FSHarvesterRpHudOverlay : Overlay
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IResourceCache _resourceCache = default!;

    private static readonly Color PointsColor = Color.FromHex("#AA44FF");
    private static readonly Color LineColor = Color.FromHex("#818CF8");

    private int _points;
    private string _pointsText = string.Empty;

    public int Points
    {
        get => _points;
        set
        {
            _points = value;
            _pointsText = Loc.GetString("fs-harvester-hud-rp", ("points", value));
        }
    }

    public string? PersonalLine;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private Font? _font;
    private Texture? _icon;
    private bool _iconLoadAttempted;

    public FSHarvesterRpHudOverlay()
    {
        IoCManager.InjectDependencies(this);
        Points = 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _font ??= new VectorFont(
            _resourceCache.GetResource<FontResource>(new ResPath("/Fonts/NotoSans/NotoSans-Bold.ttf")), 14);

        if (!_iconLoadAttempted)
        {
            _iconLoadAttempted = true;
            if (_resourceCache.TryGetResource<TextureResource>(
                    new ResPath("/Textures/_FinalStand/Interface/Research/research_icon_white_small.png"), out var icon))
            {
                _icon = icon.Texture;
            }
        }

        var screen = args.ScreenHandle;
        var screenSize = _clyde.ScreenSize;

        var rpStr = _pointsText;
        var dims = screen.GetDimensions(_font, rpStr, 1f);

        const float iconSize = 20f;
        const float iconGap = 6f;
        var hasIcon = _icon != null;
        var totalWidth = hasIcon ? iconSize + iconGap + dims.X : dims.X;

        var x = MathF.Floor(screenSize.X / 2f - totalWidth / 2f);
        var y = (float)(screenSize.Y - 160);

        var textX = x;
        if (hasIcon)
        {
            var iconY = MathF.Floor(y + (dims.Y - iconSize) / 2f);
            screen.DrawTextureRect(_icon!, UIBox2.FromDimensions(x, iconY, iconSize, iconSize));
            textX = x + iconSize + iconGap;
        }

        const float o = 1f;
        var black = Color.Black;
        screen.DrawString(_font, new Vector2(textX - o, y - o), rpStr, black);
        screen.DrawString(_font, new Vector2(textX + o, y - o), rpStr, black);
        screen.DrawString(_font, new Vector2(textX - o, y + o), rpStr, black);
        screen.DrawString(_font, new Vector2(textX + o, y + o), rpStr, black);

        screen.DrawString(_font, new Vector2(textX, y), rpStr, PointsColor);

        if (PersonalLine != null)
        {
            var personalDims = screen.GetDimensions(_font, PersonalLine, 1f);
            var px = MathF.Floor(screenSize.X / 2f - personalDims.X / 2f);
            var py = y + dims.Y + 2f;

            screen.DrawString(_font, new Vector2(px - o, py - o), PersonalLine, black);
            screen.DrawString(_font, new Vector2(px + o, py - o), PersonalLine, black);
            screen.DrawString(_font, new Vector2(px - o, py + o), PersonalLine, black);
            screen.DrawString(_font, new Vector2(px + o, py + o), PersonalLine, black);

            screen.DrawString(_font, new Vector2(px, py), PersonalLine, LineColor);
        }
    }
}
