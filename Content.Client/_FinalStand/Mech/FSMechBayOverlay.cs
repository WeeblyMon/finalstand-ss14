using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Mech;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Mech;

public sealed partial class FSMechBayOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private const float ShowRange = 8f;
    private const float IconSize = 0.42f;
    private const float Gap = 0.06f;
    private const float BelowCenter = 1.75f;
    private static readonly Color Missing = FSPalette.TextDim;
    private static readonly Color EngineeringColor = FSPalette.Money;
    private static readonly Color ScienceColor = Color.FromHex("#b48ae6");

    private SharedTransformSystem? _xform;
    private SpriteSystem? _sprite;
    private Font? _font;
    private float _fontScale;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSMechBayOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local || args.ViewportControl == null)
            return;

        _xform ??= _entMan.System<SharedTransformSystem>();
        _sprite ??= _entMan.System<SpriteSystem>();
        var s = _ui.DefaultUIScale;
        if (_font == null || Math.Abs(_fontScale - s) > 0.01f)
        {
            _fontScale = s;
            _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), (int) MathF.Round(7 * s));
        }

        var me = _xform.GetMapCoordinates(local);
        var pulse = 0.55f + 0.45f * MathF.Sin((float) _timing.RealTime.TotalSeconds * 4f);
        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var query = _entMan.EntityQueryEnumerator<FSMechBayComponent, TransformComponent>();
        while (query.MoveNext(out var bay, out var xform))
        {
            if (bay.Ready || xform.MapID != args.MapId || xform.MapID != me.MapId)
                continue;

            var world = _xform.GetWorldPosition(xform);
            if ((world - me.Position).Length() > ShowRange)
                continue;

            var center = Vector2.Transform(world, matrix);
            var tile = (Vector2.Transform(world + Vector2.UnitX, matrix) - center).Length();
            var icon = MathF.Round(IconSize * tile);
            var gap = MathF.Round(Gap * tile);
            var pip = MathF.Max(3f, MathF.Round(4f * s));
            var rowWidth = bay.Slots.Count * (icon + gap) - gap;
            var next = bay.Next();
            var caption = next is { } slot
                ? Loc.GetString("fs-mech-bay-strip-next", ("name", Loc.GetString(slot.Name).ToUpperInvariant()),
                    ("role", Loc.GetString(slot.Role == FSMechBayRole.Engineering ? "fs-mech-bay-role-engineering" : "fs-mech-bay-role-science").ToUpperInvariant()))
                : string.Empty;
            var captionSize = handle.GetDimensions(_font, caption, 1f);
            var plateWidth = MathF.Max(rowWidth, captionSize.X) + 20f * s;
            var plateHeight = icon + pip + 4f * s + captionSize.Y + 12f * s;
            var top = MathF.Round(center.Y + BelowCenter * tile - icon);
            var plateLeft = MathF.Round(center.X - plateWidth / 2f);
            var slant = MathF.Round(6f * s);

            FSShapes.Slanted(handle, new Vector2(plateLeft, top - 6f * s), plateWidth, plateHeight, slant, FSPalette.PanelDeep.WithAlpha(0.85f));
            FSShapes.Slanted(handle, new Vector2(plateLeft, top - 6f * s), plateWidth, plateHeight, slant, FSPalette.ChipEdge,
                border: MathF.Max(1f, s));

            var x = MathF.Round(center.X - rowWidth / 2f);
            foreach (var part in bay.Slots)
            {
                var installed = bay.Installed.Contains(part.Id);
                var open = !installed && bay.CanInstall(part);
                var roleColor = part.Role == FSMechBayRole.Engineering ? EngineeringColor : ScienceColor;
                var texture = _sprite.Frame0(new SpriteSpecifier.Rsi(new ResPath(bay.Sprite), part.Icon));
                var tint = installed ? Color.White : open ? Color.White.WithAlpha(0.5f + 0.4f * pulse) : Missing.WithAlpha(0.4f);
                handle.DrawTextureRect(texture, UIBox2.FromDimensions(new Vector2(x, top), new Vector2(icon, icon)), tint);

                var pipColor = installed ? FSPalette.TextBright : open ? roleColor.WithAlpha(pulse) : Color.White.WithAlpha(0.12f);
                FSShapes.Slanted(handle, new Vector2(x, top + icon + 2f * s), icon, pip, MathF.Round(pip * 0.6f), pipColor);
                x += icon + gap;
            }

            if (caption.Length > 0 && next is { } shown)
            {
                var color = shown.Role == FSMechBayRole.Engineering ? EngineeringColor : ScienceColor;
                handle.DrawString(_font, new Vector2(MathF.Round(center.X - captionSize.X / 2f), top + icon + pip + 6f * s), caption, color);
            }
        }
    }
}
