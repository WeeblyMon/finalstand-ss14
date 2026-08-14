using System.Numerics;
using Content.Shared._FinalStand.Perks;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Perks;

public sealed partial class FSPerkShopIndicatorOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan     = default!;
    [Dependency] private IResourceCache _resources  = default!;
    [Dependency] private IGameTiming    _timing     = default!;

    private Font?                  _font;
    private SharedTransformSystem? _xformSys;

    private static readonly Color Fg    = new(0.55f, 1f, 0.55f, 1f);
    private static readonly Color Black = new(0f, 0f, 0f, 0.9f);

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSPerkShopIndicatorOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        _font    ??= new VectorFont(_resources.GetResource<FontResource>(
            new ResPath("/Fonts/NotoSans/NotoSans-Bold.ttf")), 14);
        _xformSys ??= _entMan.System<SharedTransformSystem>();

        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var bob    = MathF.Sin((float)_timing.CurTime.TotalSeconds * 2.5f) * 5f;

        var query = _entMan.EntityQueryEnumerator<FSPerkShopComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var worldPos  = _xformSys.GetWorldPosition(xform);
            var screenPos = Vector2.Transform(worldPos, matrix);

            const string label = "PERKS";
            const string arrow = "▼";
            const float  o     = 1f;

            var labelDims = handle.GetDimensions(_font, label, 1f);
            var arrowDims = handle.GetDimensions(_font, arrow, 1f);

            var labelOrigin = new Vector2(screenPos.X - labelDims.X / 2f, screenPos.Y - 80f + bob);
            var arrowOrigin = new Vector2(screenPos.X - arrowDims.X / 2f, labelOrigin.Y + labelDims.Y + 2f);

            DrawOutlined(handle, _font, labelOrigin, label, o);
            DrawOutlined(handle, _font, arrowOrigin, arrow, o);
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
