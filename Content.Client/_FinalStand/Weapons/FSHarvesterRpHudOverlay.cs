using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Weapons;

// Mirrors FSWarTornHudOverlay - a screen-space overlay above the hand HUD, not an ItemStatus panel.
public sealed class FSHarvesterRpHudOverlay : Overlay
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    public int Points;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private Font? _font;
    private Texture? _icon;
    private bool _iconLoadAttempted;

    public FSHarvesterRpHudOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _font ??= new VectorFont(
            _resourceCache.GetResource<FontResource>(new ResPath("/Fonts/NotoSans/NotoSans-Bold.ttf")), 14);

        if (!_iconLoadAttempted)
        {
            _iconLoadAttempted = true;
            try
            {
                _icon = _resourceCache
                    .GetResource<TextureResource>(new ResPath("/Textures/_FinalStand/Interface/Research/research_icon_white_small.png"))
                    .Texture;
            }
            catch { }
        }

        var screen = args.ScreenHandle;
        var screenSize = _clyde.ScreenSize;

        var rpStr = $"RP: {Points}";
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

        screen.DrawString(_font, new Vector2(textX, y), rpStr, Color.FromHex("#AA44FF"));
    }
}
