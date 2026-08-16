// Bobbing label above every entity carrying TMarker. Text is fixed, so it is measured once.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.UI;

public abstract class FSWorldLabelOverlay<TMarker> : Overlay where TMarker : IComponent
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ResPath FontPath = new("/Fonts/NotoSans/NotoSans-Bold.ttf");
    private static readonly Color Outline = new(0f, 0f, 0f, 0.9f);

    private const string Arrow = "▼";
    private const float CullMargin = 96f;
    private const float OutlineOffset = 1f;

    private Font? _font;
    private SharedTransformSystem? _xform;
    private Vector2 _labelSize;
    private Vector2 _arrowSize;

    protected abstract string Label { get; }
    protected abstract int FontSize { get; }
    protected abstract float VerticalOffset { get; }
    protected abstract Color LabelColor { get; }

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    protected FSWorldLabelOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;

        if (_font == null)
        {
            _font = new VectorFont(_resources.GetResource<FontResource>(FontPath), FontSize);
            _labelSize = handle.GetDimensions(_font, Label, 1f);
            _arrowSize = handle.GetDimensions(_font, Arrow, 1f);
        }

        _xform ??= _entMan.System<SharedTransformSystem>();

        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var bounds = args.ViewportBounds;
        var bob = MathF.Sin((float) _timing.CurTime.TotalSeconds * 2.5f) * 5f;

        var query = _entMan.EntityQueryEnumerator<TMarker, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var screenPos = Vector2.Transform(_xform.GetWorldPosition(xform), matrix);

            if (screenPos.X < bounds.Left - CullMargin || screenPos.X > bounds.Right + CullMargin ||
                screenPos.Y < bounds.Top - CullMargin || screenPos.Y > bounds.Bottom + CullMargin)
                continue;

            var labelPos = new Vector2(screenPos.X - _labelSize.X / 2f, screenPos.Y - VerticalOffset + bob);
            var arrowPos = new Vector2(screenPos.X - _arrowSize.X / 2f, labelPos.Y + _labelSize.Y + 2f);

            DrawOutlined(handle, _font, labelPos, Label);
            DrawOutlined(handle, _font, arrowPos, Arrow);
        }
    }

    private void DrawOutlined(DrawingHandleScreen handle, Font font, Vector2 pos, string text)
    {
        handle.DrawString(font, pos + new Vector2(-OutlineOffset, -OutlineOffset), text, Outline);
        handle.DrawString(font, pos + new Vector2(OutlineOffset, -OutlineOffset), text, Outline);
        handle.DrawString(font, pos + new Vector2(-OutlineOffset, OutlineOffset), text, Outline);
        handle.DrawString(font, pos + new Vector2(OutlineOffset, OutlineOffset), text, Outline);
        handle.DrawString(font, pos, text, LabelColor);
    }
}
