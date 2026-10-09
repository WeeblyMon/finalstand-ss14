// A card under the RCD ghost: what the piece costs against what you have, how much of its cap is used, the build time,
// why the tile is refused, and the rotate key. A refused click shakes the card and flashes the tile red.
// Sentries and HMGs also show their reach around the ghost before you commit.
using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.HMG;
using Content.Shared._FinalStand.Materials;
using Content.Shared._FinalStand.RCD;
using Content.Shared.CCVar;
using Content.Shared.RCD;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.RCD;

public sealed partial class FSRCDGhostHintOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ResPath FontPath = new("/Fonts/NotoSans/NotoSans-Bold.ttf");
    private static readonly Color BlockedText = FSPalette.DangerSoft;
    private static readonly Color InfoText = FSPalette.Ok;
    private static readonly Color Backing = new(0f, 0f, 0f, 0.7f);
    private static readonly Color RefundText = FSPalette.Money;
    private static readonly Color CardBack = FSPalette.PanelDeep.WithAlpha(0.95f);
    private static readonly Color DeniedRed = FSPalette.Danger;
    private const float CardWidth = 172f;
    private const float Slant = 8f;
    private const float ShakeTime = 0.3f;
    private const float FlashTime = 0.45f;
    private const float HmgReach = 6f;
    private static readonly Color RangeEdge = FSPalette.TextBright.WithAlpha(0.35f);
    private static readonly Color RangeFill = FSPalette.TextBright.WithAlpha(0.05f);

    private Font? _font;
    private FSRCDLook? _look;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSRCDGhostHintOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = 10;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var hints = _entMan.System<FSRCDGhostHintSystem>();
        if (args.ViewportControl == null)
            return;

        var xform = _entMan.System<SharedTransformSystem>();
        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var since = (float) (_timing.RealTime - hints.DeniedAt).TotalSeconds;
        DrawDeniedFlash(handle, xform, matrix, hints, since, args.MapId);

        if (hints.Tile is not { } tile || hints.Text is not { } && !hints.Rotatable && hints.Piece == null)
            return;

        var world = xform.ToMapCoordinates(tile);
        if (world.MapId != args.MapId)
            return;

        var center = Vector2.Transform(world.Position, matrix);
        var tileHalf = (Vector2.Transform(world.Position + new Vector2(0f, 0.5f), matrix) - center).Length();
        if (hints.Piece is { } shown && _cfg.GetCVar(CCVars.FSEngRangePreview))
            DrawRange(handle, shown, hints.Direction, center, tileHalf * 2f);

        if (_cfg.GetCVar(CCVars.FSEngGhostCard)
            && hints.Piece is { } piece
            && _entMan.TryGetComponent<FSEngineerOnlyRCDComponent>(hints.Rcd, out var fs))
        {
            var s = _ui.DefaultUIScale;
            var shake = since < ShakeTime ? MathF.Sin(since * 70f) * 6f * s * (1f - since / ShakeTime) : 0f;
            DrawCard(handle, hints, piece, fs, new Vector2(center.X + shake, center.Y + tileHalf + 6f * s), s);
            return;
        }

        DrawStrip(handle, hints, center, tileHalf);
    }

    // A faint ring for a sentry's range, or the HMG's traverse wedge in the direction it will face.
    private void DrawRange(DrawingHandleScreen handle, RCDPrototype piece, Direction direction, Vector2 center, float tile)
    {
        if (piece.Prototype is not { } id || !_proto.TryIndex<EntityPrototype>(id, out var proto))
            return;

        if (proto.TryGetComponent<FSSentryTurretComponent>(out var sentry))
        {
            var radius = sentry.Range * tile;
            FSShapes.Disc(handle, center, radius, RangeFill);
            FSShapes.Ring(handle, center, radius, 1.5f, RangeEdge);
            return;
        }

        if (!proto.TryGetComponent<FSHMGComponent>(out var hmg))
            return;

        var facing = (float) (direction.ToAngle() - Math.PI / 2).Degrees;
        var half = hmg.Arc / 2f;
        var reach = HmgReach * tile;
        var fan = new Vector2[26];
        fan[0] = center;
        for (var i = 0; i <= 24; i++)
        {
            var a = MathHelper.DegreesToRadians(facing - half + hmg.Arc * i / 24f);
            fan[i + 1] = center + new Vector2(MathF.Cos((float) a), -MathF.Sin((float) a)) * reach;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, fan, RangeFill);
        FSShapes.Arc(handle, center, reach, 1.5f, -(facing + half), -(facing - half), RangeEdge);
        handle.DrawLine(center, fan[1], RangeEdge);
        handle.DrawLine(center, fan[^1], RangeEdge);
    }

    private void DrawDeniedFlash(DrawingHandleScreen handle, SharedTransformSystem xform, Matrix3x2 matrix,
        FSRCDGhostHintSystem hints, float since, Robust.Shared.Map.MapId map)
    {
        if (since >= FlashTime || hints.DeniedTile is not { } denied || !denied.IsValid(_entMan))
            return;

        var world = xform.ToMapCoordinates(denied);
        if (world.MapId != map)
            return;

        var a = Vector2.Transform(world.Position + new Vector2(-0.5f, 0.5f), matrix);
        var b = Vector2.Transform(world.Position + new Vector2(0.5f, -0.5f), matrix);
        var box = new UIBox2(Vector2.Min(a, b), Vector2.Max(a, b));
        var fade = 1f - since / FlashTime;
        handle.DrawRect(box, DeniedRed.WithAlpha(0.5f * fade));
        handle.DrawRect(box, DeniedRed.WithAlpha(fade), false);
        handle.DrawRect(new UIBox2(box.TopLeft + Vector2.One, box.BottomRight - Vector2.One), DeniedRed.WithAlpha(fade), false);
    }

    private void DrawCard(DrawingHandleScreen handle, FSRCDGhostHintSystem hints, RCDPrototype piece,
        FSEngineerOnlyRCDComponent fs, Vector2 topCenter, float s)
    {
        _look ??= new FSRCDLook(_resources, _proto, _entMan.System<SpriteSystem>());
        var name = _look.Bold(9.5f, s);
        var mono = _look.Mono(8.5f, s);
        var small = _look.Bold(7f, s);
        var pad = 8f * s;
        var width = MathF.Round(CardWidth * s);
        var slant = MathF.Round(Slant * s);
        var line = name.GetLineHeight(1f);
        var monoLine = mono.GetLineHeight(1f);
        var icon = MathF.Round(14f * s);

        _proto.TryIndex<FSBuildRecipePrototype>(piece.ID, out var recipe);
        var costs = piece.Mode == RcdMode.Deconstruct ? null : recipe;
        (int Used, int Max)? cap = recipe?.CapGroup is { } group && fs.CapsMax.TryGetValue(group, out var max)
            ? (fs.CapsUsed.GetValueOrDefault(group), max)
            : null;
        var locked = fs.Locked.Contains(piece.ID);
        var banner = locked ? Loc.GetString("fs-rcd-locked") : hints.Text;
        var refused = locked || hints.Blocked;

        var height = pad + line;
        if (costs is { Materials.Count: > 0 })
            height += 4f * s + MathF.Max(icon, monoLine);
        if (cap != null)
            height += 6f * s + monoLine;
        if (banner != null)
            height += 6f * s + monoLine + 4f * s;
        if (hints.Rotatable)
            height += 6f * s + monoLine;
        height = MathF.Round(height + pad);

        var left = MathF.Round(topCenter.X - width / 2f);
        var top = MathF.Round(topCenter.Y);
        var edge = refused ? DeniedRed.WithAlpha(0.85f) : FSPalette.ChipEdge;
        FSShapes.Slanted(handle, new Vector2(left, top), width, height, slant, CardBack);
        FSShapes.Slanted(handle, new Vector2(left, top), width, height, slant, edge, border: MathF.Max(1f, s));
        FSShapes.Slanted(handle, new Vector2(left + 2f * s, top), slant + 3f * s, height, slant,
            refused ? DeniedRed : FSPalette.Money);

        var x = left + slant + pad;
        var right = left + width - slant / 2f - pad;
        var y = top + pad;
        var timeWidth = 0f;
        if (piece.Mode != RcdMode.Deconstruct)
        {
            var time = Loc.GetString("fs-rcd-card-time", ("seconds", piece.Delay));
            timeWidth = handle.GetDimensions(mono, time, 1f).X + 6f * s;
            handle.DrawString(mono, new Vector2(right - timeWidth + 6f * s, y + (line - monoLine) / 2f), time, FSPalette.TextMuted);
        }
        handle.DrawString(name, new Vector2(x, y), Truncate(handle, name, FSRCDText.Name(_proto, piece), right - x - timeWidth), FSPalette.TextBright);
        y += line;

        if (costs is { Materials.Count: > 0 })
        {
            y += 4f * s;
            var carried = _entMan.System<FSCarriedMaterialSystem>().CountAll(_player.LocalEntity);
            var cx = x;
            foreach (var (stack, need) in costs.Materials)
            {
                var have = FSRCDText.Have(carried, fs, stack);
                if (_look.MaterialIcon(stack) is { } tex)
                    handle.DrawTextureRect(tex, UIBox2.FromDimensions(new Vector2(cx, y), new Vector2(icon, icon)));
                cx += icon + 3f * s;
                var haveText = have.ToString();
                var textY = y + (icon - monoLine) / 2f;
                handle.DrawString(mono, new Vector2(cx, textY), haveText, have >= need ? FSPalette.Ok : FSPalette.DangerSoft);
                cx += handle.GetDimensions(mono, haveText, 1f).X;
                var needText = $"/{need}";
                handle.DrawString(mono, new Vector2(cx, textY), needText, FSPalette.TextMuted);
                cx += handle.GetDimensions(mono, needText, 1f).X + 10f * s;
            }

            y += MathF.Max(icon, monoLine);
        }

        if (cap is { } c)
        {
            y += 6f * s;
            var label = Loc.GetString("fs-rcd-card-cap");
            handle.DrawString(small, new Vector2(x, y + (monoLine - small.GetLineHeight(1f)) / 2f), label, FSPalette.TextMuted);
            var count = $"{c.Used}/{c.Max}";
            var countWidth = handle.GetDimensions(mono, count, 1f).X;
            var full = c.Used >= c.Max;
            handle.DrawString(mono, new Vector2(right - countWidth, y), count, full ? FSPalette.DangerSoft : FSPalette.TextBright);
            var railLeft = x + handle.GetDimensions(small, label, 1f).X + 6f * s;
            var railWidth = right - countWidth - 6f * s - railLeft;
            if (railWidth > 8f)
            {
                var fraction = c.Max > 0 ? Math.Clamp((float) c.Used / c.Max, 0f, 1f) : 1f;
                FSWorldChip.Rail(handle, new Vector2(railLeft, MathF.Round(y + monoLine / 2f - 1.5f * s)), railWidth,
                    MathF.Max(2f, MathF.Round(3f * s)), fraction, full ? FSPalette.Danger : FSPalette.Money);
            }

            y += monoLine;
        }

        if (banner != null)
        {
            y += 6f * s;
            var bannerHeight = monoLine + 4f * s;
            var color = refused ? FSPalette.TextBright : hints.Deconstructing ? RefundText : InfoText;
            if (refused)
                FSShapes.Slanted(handle, new Vector2(x - 4f * s, y), right - x + 8f * s, bannerHeight, MathF.Round(4f * s),
                    DeniedRed.WithAlpha(0.55f));
            handle.DrawString(mono, new Vector2(x, y + 2f * s), Truncate(handle, mono, banner, right - x), color);
            y += bannerHeight;
        }

        if (hints.Rotatable)
        {
            y += 6f * s;
            var key = _input.TryGetKeyBinding(EngineKeyFunctions.EditorRotateObject, out var binding)
                ? binding.GetKeyString()
                : "R";
            var keyWidth = MathF.Max(monoLine, handle.GetDimensions(small, key, 1f).X + 6f * s);
            var box = UIBox2.FromDimensions(new Vector2(x, y), new Vector2(keyWidth, monoLine));
            handle.DrawRect(box, FSPalette.ChipBack);
            handle.DrawRect(box, FSPalette.ChipEdge, false);
            var keySize = handle.GetDimensions(small, key, 1f);
            handle.DrawString(small, new Vector2(box.Center.X - keySize.X / 2f, box.Center.Y - keySize.Y / 2f), key, FSPalette.TextBright);
            handle.DrawString(mono, new Vector2(x + keyWidth + 5f * s, y), Loc.GetString("fs-rcd-card-rotate"), FSPalette.TextMuted);
        }
    }

    private void DrawStrip(DrawingHandleScreen handle, FSRCDGhostHintSystem hints, Vector2 center, float tileHalf)
    {
        _font ??= new VectorFont(_resources.GetResource<FontResource>(FontPath), 10);
        var y = center.Y + tileHalf + 4f;
        if (hints.Text is { } text)
        {
            var size = handle.GetDimensions(_font, text, 1f);
            var pos = new Vector2(center.X - size.X / 2f, y);
            handle.DrawRect(new UIBox2(pos - new Vector2(4f, 2f), pos + size + new Vector2(4f, 2f)), Backing);
            handle.DrawString(_font, pos, text, hints.Blocked ? BlockedText : hints.Deconstructing ? RefundText : InfoText);
            y += size.Y + 6f;
        }

        if (!hints.Rotatable)
            return;

        var rotate = Loc.GetString("fs-rcd-rotate-hint");
        var rotateSize = handle.GetDimensions(_font, rotate, 1f);
        var rotatePos = new Vector2(center.X - rotateSize.X / 2f, y);
        handle.DrawRect(new UIBox2(rotatePos - new Vector2(4f, 2f), rotatePos + rotateSize + new Vector2(4f, 2f)), Backing);
        handle.DrawString(_font, rotatePos, rotate, FSPalette.TextMuted);
    }

    private static string Truncate(DrawingHandleScreen handle, Font font, string text, float max)
    {
        if (handle.GetDimensions(font, text, 1f).X <= max)
            return text;

        for (var len = text.Length - 1; len > 0; len--)
        {
            var cut = text.Substring(0, len) + "…";
            if (handle.GetDimensions(font, cut, 1f).X <= max)
                return cut;
        }

        return text;
    }
}
