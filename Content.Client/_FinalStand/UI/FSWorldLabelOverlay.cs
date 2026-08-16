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
    private Vector2 _constantLabelSize;
    private Vector2 _arrowSize;

    protected abstract string Label { get; }
    protected abstract int FontSize { get; }
    protected abstract float VerticalOffset { get; }
    protected abstract Color LabelColor { get; }

    protected virtual bool DynamicLabel => false;
    protected virtual bool ShowArrow => true;
    protected virtual bool Bob => true;

    protected virtual string GetLabel(EntityUid uid, TMarker marker) => Label;

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
            _constantLabelSize = handle.GetDimensions(_font, Label, 1f);
            _arrowSize = handle.GetDimensions(_font, Arrow, 1f);
        }

        _xform ??= _entMan.System<SharedTransformSystem>();

        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var bounds = args.ViewportBounds;
        var bob = Bob ? MathF.Sin((float) _timing.CurTime.TotalSeconds * 2.5f) * 5f : 0f;

        var query = _entMan.EntityQueryEnumerator<TMarker, TransformComponent>();
        while (query.MoveNext(out var uid, out var marker, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var screenPos = Vector2.Transform(_xform.GetWorldPosition(xform), matrix);

            if (screenPos.X < bounds.Left - CullMargin || screenPos.X > bounds.Right + CullMargin ||
                screenPos.Y < bounds.Top - CullMargin || screenPos.Y > bounds.Bottom + CullMargin)
                continue;

            var label = DynamicLabel ? GetLabel(uid, marker) : Label;
            var labelSize = DynamicLabel ? handle.GetDimensions(_font, label, 1f) : _constantLabelSize;

            var labelPos = new Vector2(screenPos.X - labelSize.X / 2f, screenPos.Y - VerticalOffset + bob);
            DrawOutlined(handle, _font, labelPos, label);

            if (!ShowArrow)
                continue;

            var arrowPos = new Vector2(screenPos.X - _arrowSize.X / 2f, labelPos.Y + labelSize.Y + 2f);
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
