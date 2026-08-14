using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Upgrades;

public sealed partial class FSBattleTranceHudOverlay : Overlay
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IResourceCache _resourceCache = default!;

    public int Stacks    = 0;
    public int MaxStacks = 15;
    public int BonusPct  = 0;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private Font?    _font;
    private Texture? _skullTexture;
    private bool     _skullLoadAttempted;

    public FSBattleTranceHudOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (Stacks <= 0)
            return;

        _font ??= new VectorFont(
            _resourceCache.GetResource<FontResource>(new ResPath("/Fonts/NotoSans/NotoSans-Bold.ttf")), 14);

        if (!_skullLoadAttempted)
        {
            _skullLoadAttempted = true;
            try
            {
                _skullTexture = _resourceCache
                    .GetResource<TextureResource>(new ResPath("/Textures/_FinalStand/Interface/BattleTrance/skull.png"))
                    .Texture;
            }
            catch { }
        }

        var screen     = args.ScreenHandle;
        var screenSize = _clyde.ScreenSize;

        var streakStr = $"{Stacks} / {MaxStacks}";
        var bonusStr  = $"  +{BonusPct}%";

        var streakDims = screen.GetDimensions(_font, streakStr, 1f);
        var bonusDims  = screen.GetDimensions(_font, bonusStr, 1f);
        var totalText  = streakDims.X + bonusDims.X;

        const float iconSize = 40f;
        const float iconGap  = 6f;
        var hasIcon    = _skullTexture != null;
        var totalWidth = hasIcon ? iconSize + iconGap + totalText : totalText;

        var x = MathF.Floor(screenSize.X / 2f - totalWidth / 2f);
        var y = (float)(screenSize.Y - 160);

        // Skull icon (if loaded)
        var textX = x;
        if (hasIcon)
        {
            var iconY = MathF.Floor(y + (streakDims.Y - iconSize) / 2f);
            screen.DrawTextureRect(_skullTexture!, UIBox2.FromDimensions(x, iconY, iconSize, iconSize));
            textX = x + iconSize + iconGap;
        }

        // Outline pass
        var fullStr = streakStr + bonusStr;
        const float o = 1f;
        var black = Color.Black;
        screen.DrawString(_font, new Vector2(textX - o, y - o), fullStr, black);
        screen.DrawString(_font, new Vector2(textX + o, y - o), fullStr, black);
        screen.DrawString(_font, new Vector2(textX - o, y + o), fullStr, black);
        screen.DrawString(_font, new Vector2(textX + o, y + o), fullStr, black);

        // Streak count in white, bonus in orange-red
        screen.DrawString(_font, new Vector2(textX,               y), streakStr, Color.White);
        screen.DrawString(_font, new Vector2(textX + streakDims.X, y), bonusStr, Color.FromHex("#FF6B35"));
    }
}
