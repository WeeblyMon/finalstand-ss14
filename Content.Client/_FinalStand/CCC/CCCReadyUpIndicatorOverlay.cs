using System.Numerics;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.CCC;

public sealed class CCCReadyUpIndicatorOverlay : Overlay
{
    [Dependency] private readonly IResourceCache      _resources = default!;
    [Dependency] private readonly IGameTiming         _timing    = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private Font? _font;

    private static readonly Color Fg    = Color.FromHex("#7EB8D4");
    private static readonly Color Black = new(0f, 0f, 0f, 0.85f);

    public bool ShowReminder = false;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CCCReadyUpIndicatorOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!ShowReminder)
            return;

        _font ??= new VectorFont(_resources.GetResource<FontResource>(
            new ResPath("/Fonts/NotoSans/NotoSans-Bold.ttf")), 12);

        var handle = args.ScreenHandle;
        var bob    = MathF.Sin((float)_timing.CurTime.TotalSeconds * 2.5f) * 3f;

        const string line1 = "READY UP";
        const string arrow = "▼";
        const float  o     = 1f;

        // Resolve the PDA/ID slot button screen position dynamically so the
        // indicator tracks the actual slot at any resolution or UI scale.
        float centerX;
        float baseY;

        var hotbarGui = _uiManager.GetActiveUIWidgetOrNull<HotbarGui>();
        if (hotbarGui?.SecondHotbar != null &&
            hotbarGui.SecondHotbar.TryGetButton("id", out var pdaButton))
        {
            var btnCenter = pdaButton.GlobalPixelPosition + pdaButton.PixelSize / 2;
            centerX = btnCenter.X;
            baseY   = pdaButton.GlobalPixelPosition.Y - 44f + bob;
        }
        else
        {
            // Fallback: no-op until the hotbar is visible.
            return;
        }

        var line1Dims = handle.GetDimensions(_font, line1, 1f);
        var arrowDims = handle.GetDimensions(_font, arrow, 1f);

        var line1Pos = new Vector2(centerX - line1Dims.X / 2f, baseY);
        var arrowPos = new Vector2(centerX - arrowDims.X / 2f, baseY + line1Dims.Y + 2f);

        DrawOutlined(handle, _font, line1Pos, line1, o);
        DrawOutlined(handle, _font, arrowPos, arrow, o);
    }

    private static void DrawOutlined(DrawingHandleScreen handle, Font font, Vector2 pos, string text, float o)
    {
        handle.DrawString(font, pos + new Vector2(-o, -o), text, Black);
        handle.DrawString(font, pos + new Vector2( o, -o), text, Black);
        handle.DrawString(font, pos + new Vector2(-o,  o), text, Black);
        handle.DrawString(font, pos + new Vector2( o,  o), text, Black);
        handle.DrawString(font, pos, text, Fg);
    }
}
