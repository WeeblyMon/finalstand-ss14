using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Upgrades;

public sealed class FSMarksmansRhythmHudOverlay : Overlay
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    public int Stacks    = 0;
    public int MaxStacks = 20;
    public int BonusPct  = 0;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private Font? _font;

    public FSMarksmansRhythmHudOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (Stacks <= 0)
            return;

        _font ??= new VectorFont(
            _resourceCache.GetResource<FontResource>(new ResPath("/Fonts/NotoSans/NotoSans-Bold.ttf")), 14);

        var screen     = args.ScreenHandle;
        var screenSize = _clyde.ScreenSize;

        var stackStr = $"{Stacks} / {MaxStacks}";
        var bonusStr = $"  +{BonusPct}%";

        var stackDims = screen.GetDimensions(_font, stackStr, 1f);
        var bonusDims = screen.GetDimensions(_font, bonusStr, 1f);
        var totalWidth = stackDims.X + bonusDims.X;

        var x = MathF.Floor(screenSize.X / 2f - totalWidth / 2f);
        var y = (float)(screenSize.Y - 200);

        var fullStr = stackStr + bonusStr;
        const float o = 1f;
        var black = Color.Black;
        screen.DrawString(_font, new Vector2(x - o, y - o), fullStr, black);
        screen.DrawString(_font, new Vector2(x + o, y - o), fullStr, black);
        screen.DrawString(_font, new Vector2(x - o, y + o), fullStr, black);
        screen.DrawString(_font, new Vector2(x + o, y + o), fullStr, black);

        screen.DrawString(_font, new Vector2(x, y), stackStr, Color.White);
        screen.DrawString(_font, new Vector2(x + stackDims.X, y), bonusStr, Color.FromHex("#FF6B35"));
    }
}
