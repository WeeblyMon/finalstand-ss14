using System.Numerics;
using Content.Shared._FinalStand.Teleportation;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Teleportation;

public sealed partial class FSPortalTimerOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IResourceCache _resources = default!;

    private static readonly Color Outline = new(0f, 0f, 0f, 0.9f);
    private static readonly Color BarBack = new(0.08f, 0.08f, 0.1f, 0.85f);
    private static readonly Color PausedColor = Color.FromHex("#9fc8ff");
    private static readonly Vector2 WorldOffset = new(0f, 0.75f);
    private const float BarWidth = 34f;
    private const float BarHeight = 4f;

    private Font? _font;
    private SharedTransformSystem? _xform;
    private FSExpiringPortalSystem? _portals;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSPortalTimerOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        _font ??= new VectorFont(_resources.GetResource<FontResource>(new ResPath("/Fonts/NotoSans/NotoSans-Bold.ttf")), 11);
        _xform ??= _entMan.System<SharedTransformSystem>();
        _portals ??= _entMan.System<FSExpiringPortalSystem>();

        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();

        var query = _entMan.EntityQueryEnumerator<FSExpiringPortalComponent, TransformComponent>();
        while (query.MoveNext(out var portal, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var left = _portals.SecondsLeft(portal.ExpiresAt, portal.PausedLeft);
            var paused = portal.PausedLeft != null;
            var color = paused ? PausedColor
                : left <= 5f ? Color.Red
                : left <= 15f ? Color.Orange
                : Color.White;

            var anchor = Vector2.Transform(_xform.GetWorldPosition(xform) + WorldOffset, matrix);

            var fraction = portal.Lifetime > 0f ? Math.Clamp(left / portal.Lifetime, 0f, 1f) : 0f;
            var bar = UIBox2.FromDimensions(anchor.X - BarWidth / 2f, anchor.Y, BarWidth, BarHeight);
            handle.DrawRect(new UIBox2(bar.Left - 1f, bar.Top - 1f, bar.Right + 1f, bar.Bottom + 1f), BarBack);
            handle.DrawRect(new UIBox2(bar.Left, bar.Top, bar.Left + BarWidth * fraction, bar.Bottom), color);

            var seconds = ((int) MathF.Ceiling(left)).ToString();
            var text = paused ? Loc.GetString("fs-portal-timer-paused", ("seconds", seconds)) : seconds;

            var dims = handle.GetDimensions(_font, text, 1f);
            var origin = new Vector2(anchor.X - dims.X / 2f, anchor.Y - dims.Y - 1f);
            handle.DrawString(_font, origin + new Vector2(-1f, 0f), text, Outline);
            handle.DrawString(_font, origin + new Vector2(1f, 0f), text, Outline);
            handle.DrawString(_font, origin + new Vector2(0f, -1f), text, Outline);
            handle.DrawString(_font, origin + new Vector2(0f, 1f), text, Outline);
            handle.DrawString(_font, origin, text, color);
        }
    }
}
