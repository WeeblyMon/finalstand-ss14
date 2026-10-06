using System.Numerics;
using Content.Client._FinalStand.Interface;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.RCD;

public sealed partial class FSRCDGhostHintOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IResourceCache _resources = default!;

    private static readonly ResPath FontPath = new("/Fonts/NotoSans/NotoSans-Bold.ttf");
    private static readonly Color BlockedText = FSPalette.DangerSoft;
    private static readonly Color InfoText = FSPalette.Ok;
    private static readonly Color Backing = new(0f, 0f, 0f, 0.7f);
    private static readonly Color RefundText = FSPalette.Money;

    private Font? _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSRCDGhostHintOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var hints = _entMan.System<FSRCDGhostHintSystem>();
        if (args.ViewportControl == null || hints.Tile is not { } tile || hints.Text is not { } text)
            return;

        var xform = _entMan.System<SharedTransformSystem>();
        var world = xform.ToMapCoordinates(tile);
        if (world.MapId != args.MapId)
            return;

        _font ??= new VectorFont(_resources.GetResource<FontResource>(FontPath), 10);
        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var center = Vector2.Transform(world.Position, matrix);
        var tileHalf = (Vector2.Transform(world.Position + new Vector2(0f, 0.5f), matrix) - center).Length();

        var size = handle.GetDimensions(_font, text, 1f);
        var pos = new Vector2(center.X - size.X / 2f, center.Y + tileHalf + 4f);
        handle.DrawRect(new UIBox2(pos - new Vector2(4f, 2f), pos + size + new Vector2(4f, 2f)), Backing);
        handle.DrawString(_font, pos, text, hints.Blocked ? BlockedText : hints.Deconstructing ? RefundText : InfoText);
    }
}
