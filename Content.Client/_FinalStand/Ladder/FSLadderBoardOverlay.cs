using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Ladder;

public sealed partial class FSLadderBoardOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IResourceCache _resources = default!;

    private static readonly ResPath BoldPath = new("/Fonts/NotoSans/NotoSans-Bold.ttf");
    private static readonly ResPath RegularPath = new("/Fonts/NotoSans/NotoSans-Regular.ttf");
    private static readonly Color[] Medals =
    {
        Color.FromHex("#e3b448"),
        Color.FromHex("#c9ccd1"),
        Color.FromHex("#c27c48"),
    };

    private const float Width = 290f;
    private const float Pad = 8f;
    private const float RowGap = 3f;
    private const float RankWidth = 24f;
    private const float ResultWidth = 70f;
    private const float TimeWidth = 58f;
    private const float WorldLift = 0.7f;
    private const float CullMargin = 160f;
    private const string Ellipsis = "…";

    private Font? _title;
    private Font? _body;
    private SharedTransformSystem? _xform;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSLadderBoardOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = -1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        _title ??= new VectorFont(_resources.GetResource<FontResource>(BoldPath), 12);
        _body ??= new VectorFont(_resources.GetResource<FontResource>(RegularPath), 10);
        _xform ??= _entMan.System<SharedTransformSystem>();

        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var bounds = args.ViewportBounds;

        var query = _entMan.EntityQueryEnumerator<FSLadderBoardComponent, TransformComponent>();
        while (query.MoveNext(out var board, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var anchor = Vector2.Transform(_xform.GetWorldPosition(xform) + new Vector2(0f, WorldLift), matrix);
            if (anchor.X < bounds.Left - CullMargin || anchor.X > bounds.Right + CullMargin ||
                anchor.Y < bounds.Top - CullMargin || anchor.Y > bounds.Bottom + CullMargin * 2)
                continue;

            DrawBoard(handle, board, anchor);
        }
    }

    private void DrawBoard(DrawingHandleScreen handle, FSLadderBoardComponent board, Vector2 anchor)
    {
        var title = board.Weekly
            ? Loc.GetString("fs-ladder-board-weekly-title")
            : Loc.GetString("fs-ladder-board-title");
        var subtitle = board.Weekly
            ? board.WeeklyName ?? Loc.GetString("fs-ladder-weekly")
            : Loc.GetString("fs-ladder-board-ladder",
                ("difficulty", Loc.GetString(FSDifficultyRules.NameLocId(board.Difficulty))),
                ("bracket", Loc.GetString(board.AllBrackets ? "fs-ladder-bracket-all" : FSLadder.BracketLocId(board.Bracket))));

        var titleHeight = _title!.GetLineHeight(1f);
        var lineHeight = _body!.GetLineHeight(1f);
        var rows = Math.Max(1, board.Runs.Count);
        var height = Pad + titleHeight + lineHeight + 6f + rows * (lineHeight + RowGap) + Pad - RowGap;

        var box = UIBox2.FromDimensions(new Vector2(anchor.X - Width / 2f, anchor.Y - height), new Vector2(Width, height));
        handle.DrawRect(box.Translated(new Vector2(2f, 3f)), FSPalette.PanelShadow);
        handle.DrawRect(box, FSPalette.PanelDeep);
        handle.DrawRect(box, FSPalette.PanelEdge, false);
        handle.DrawRect(new UIBox2(box.Left, box.Top, box.Right, box.Top + 2f), FSPalette.Money);

        var y = box.Top + Pad;
        DrawCentered(handle, _title, title, box, y, FSPalette.Money);
        y += titleHeight;
        DrawCentered(handle, _body, Fit(handle, _body, subtitle, Width - Pad * 2), box, y, FSPalette.TextMuted);
        y += lineHeight + 6f;

        if (board.Runs.Count == 0)
        {
            DrawCentered(handle, _body, Loc.GetString("fs-ladder-board-empty"), box, y, FSPalette.TextDim);
            return;
        }

        var nameWidth = Width - Pad * 2 - RankWidth - ResultWidth - TimeWidth;
        for (var i = 0; i < board.Runs.Count; i++)
        {
            var run = board.Runs[i];
            var rankColor = i < Medals.Length ? Medals[i] : FSPalette.TextMuted;
            var x = box.Left + Pad;

            handle.DrawString(_body, new Vector2(x, y), $"#{i + 1}", rankColor);
            x += RankWidth;

            var result = Loc.GetString(run.Victory ? "fs-ladder-board-row-victory" : "fs-ladder-board-row-wave",
                ("waves", run.Waves));
            handle.DrawString(_body, new Vector2(x, y), result, run.Victory ? FSPalette.Ok : FSPalette.TextBright);
            x += ResultWidth;

            handle.DrawString(_body, new Vector2(x, y),
                TimeSpan.FromSeconds(run.DurationSeconds).ToString(@"h\:mm\:ss"), FSPalette.TextDim);
            x += TimeWidth;

            handle.DrawString(_body, new Vector2(x, y), Fit(handle, _body, run.Players, nameWidth),
                i == 0 ? FSPalette.TextBright : FSPalette.TextMuted);
            y += lineHeight + RowGap;
        }
    }

    private static void DrawCentered(DrawingHandleScreen handle, Font font, string text, UIBox2 box, float y, Color color)
    {
        var width = handle.GetDimensions(font, text, 1f).X;
        handle.DrawString(font, new Vector2(box.Left + (box.Width - width) / 2f, y), text, color);
    }

    private static string Fit(DrawingHandleScreen handle, Font font, string text, float maxWidth)
    {
        if (handle.GetDimensions(font, text, 1f).X <= maxWidth)
            return text;

        var length = text.Length;
        while (length > 0 && handle.GetDimensions(font, text[..length] + Ellipsis, 1f).X > maxWidth)
            length--;
        return text[..length].TrimEnd(' ', ',') + Ellipsis;
    }
}
