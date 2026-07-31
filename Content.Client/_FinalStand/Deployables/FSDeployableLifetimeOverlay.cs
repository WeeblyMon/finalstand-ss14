using System.Numerics;
using Content.Shared._FinalStand.Deployables;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Deployables;

public sealed class FSDeployableLifetimeOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan    = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IGameTiming    _timing    = default!;

    private Font?                  _font;
    private SharedTransformSystem? _xformSys;

    private static readonly Color Fg    = Color.White;
    private static readonly Color Black = new(0f, 0f, 0f, 0.9f);

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSDeployableLifetimeOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        _font    ??= new VectorFont(_resources.GetResource<FontResource>(
            new ResPath("/Fonts/NotoSans/NotoSans-Bold.ttf")), 8);
        _xformSys ??= _entMan.System<SharedTransformSystem>();

        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var now    = _timing.CurTime;

        var query = _entMan.EntityQueryEnumerator<FSDeployableLifetimeComponent, TransformComponent>();
        while (query.MoveNext(out _, out var lifetime, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var remaining = lifetime.ExpiresAt - now;
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;

            var label = $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2}";

            var worldPos  = _xformSys.GetWorldPosition(xform);
            var screenPos = Vector2.Transform(worldPos, matrix);

            var labelDims  = handle.GetDimensions(_font, label, 1f);
            var labelOrigin = new Vector2(screenPos.X - labelDims.X / 2f, screenPos.Y - 50f);

            DrawOutlined(handle, _font, labelOrigin, label, 1f);
        }
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
