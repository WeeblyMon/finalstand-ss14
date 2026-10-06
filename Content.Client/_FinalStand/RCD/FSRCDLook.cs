// Shared look for the Build Ring and Build Belt: palette, fonts at UI scale, circles, icons and the cost block.
using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Shared._FinalStand.RCD;
using Content.Shared.RCD;
using Content.Shared.Stacks;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.RCD;

public enum FSRCDEntryKind : byte
{
    Piece,
    Deconstruct,
    Station,
    Back,
}

public sealed record FSRCDEntry(FSRCDEntryKind Kind, RCDPrototype? Piece, Texture? Icon, int Affordable, bool Short,
    (int Used, int Max)? Cap, bool Selected, bool Locked = false);

public sealed class FSRCDLook
{
    public static readonly Color Panel = FSPalette.PanelBack;
    public static readonly Color PanelDeep = FSPalette.PanelDeep;
    public static readonly Color Edge = FSPalette.ChipEdge;
    public static readonly Color Fg = FSPalette.TextBright;
    public static readonly Color Muted = FSPalette.TextMuted;
    public static readonly Color Gold = FSPalette.Money;
    public static readonly Color Good = FSPalette.Ok;
    public static readonly Color Bad = FSPalette.DangerSoft;
    public static readonly Color DeconBack = Color.FromHex("#2a1514").WithAlpha(0.88f);
    public static readonly Color DeconSelected = Color.FromHex("#3a1a18").WithAlpha(0.92f);
    public static readonly Color DeconEdge = FSPalette.Danger;
    public static readonly Color SelectedBack = FSPalette.ButtonPressBack;

    private readonly IResourceCache _cache;
    private readonly IPrototypeManager _proto;
    private readonly SpriteSystem _sprite;
    private readonly Dictionary<(string, int), Font> _fonts = new();
    private Texture? _lock;

    public Texture LockIcon => _lock ??= _cache.GetResource<TextureResource>("/Textures/Interface/VerbIcons/lock.svg.192dpi.png").Texture;

    public void DrawLock(DrawingHandleScreen handle, Vector2 center, float side)
    {
        handle.DrawTextureRect(LockIcon, UIBox2.FromDimensions(center - new Vector2(side / 2f), new Vector2(side)), Fg);
    }

    public FSRCDLook(IResourceCache cache, IPrototypeManager proto, SpriteSystem sprite)
    {
        _cache = cache;
        _proto = proto;
        _sprite = sprite;
    }

    public Font Bold(float pt, float scale) => Get("/Fonts/NotoSans/NotoSans-Bold.ttf", pt, scale);
    public Font Body(float pt, float scale) => Get("/Fonts/NotoSans/NotoSans-Regular.ttf", pt, scale);
    public Font Mono(float pt, float scale) => Get("/Fonts/RobotoMono/RobotoMono-Regular.ttf", pt, scale);

    private Font Get(string path, float pt, float scale)
    {
        var size = Math.Max(6, (int) MathF.Round(pt * scale));
        if (!_fonts.TryGetValue((path, size), out var font))
            _fonts[(path, size)] = font = new VectorFont(_cache.GetResource<FontResource>(path), size);
        return font;
    }

    public static void Circle(DrawingHandleScreen handle, Vector2 center, float radius, Color color, bool filled = true)
    {
        handle.DrawCircle(center, radius, Color.ToSrgb(color), filled);
    }

    public static void RingEdge(DrawingHandleScreen handle, Vector2 center, float radius, float width, Color color)
    {
        for (var w = 0f; w < width; w += 0.5f)
            Circle(handle, center, radius - w, color, filled: false);
    }

    public Texture? MaterialIcon(ProtoId<StackPrototype> stack)
    {
        var proto = _proto.Index(stack);
        if (proto.Icon is { } icon)
            return _sprite.Frame0(icon);

        return _sprite.GetPrototypeIcon(proto.Spawn.Id).Default;
    }

    public string MaterialName(ProtoId<StackPrototype> stack) => Loc.GetString(_proto.Index(stack).Name).ToLowerInvariant();

    // Builds the entries both pickers show, with have/need worked out once.
    public static List<FSRCDEntry> Entries(IPrototypeManager proto, SpriteSystem sprite,
        IReadOnlyDictionary<ProtoId<StackPrototype>, int> carried, FSEngineerOnlyRCDComponent fs, string current, IEnumerable<RCDPrototype> pieces)
    {
        var list = new List<FSRCDEntry>();
        foreach (var piece in pieces)
        {
            var affordable = FSRCDPalette.Affordable(proto, carried, fs, piece);
            (int, int)? cap = null;
            var capped = false;
            if (proto.TryIndex<FSBuildRecipePrototype>(piece.ID, out var recipe)
                && recipe.CapGroup is { } group
                && fs.CapsMax.TryGetValue(group, out var max))
            {
                var used = fs.CapsUsed.GetValueOrDefault(group);
                cap = (used, max);
                capped = used >= max;
            }

            var locked = fs.Locked.Contains(piece.ID);
            list.Add(new FSRCDEntry(
                piece.Mode == RcdMode.Deconstruct ? FSRCDEntryKind.Deconstruct : FSRCDEntryKind.Piece,
                piece,
                piece.Sprite is { } spec ? sprite.Frame0(spec) : null,
                affordable,
                affordable == 0 && !capped && !locked,
                cap,
                piece.ID == current,
                locked));
        }

        return list;
    }

    // Name, a one-line role, one row per material (icon, have in green or red, / need), then cap and build time.
    public float DrawInfo(DrawingHandleScreen handle, IReadOnlyDictionary<ProtoId<StackPrototype>, int> carried, FSEngineerOnlyRCDComponent fs,
        RCDPrototype piece, Vector2 topCenter, float scale, bool centered, float maxWidth)
    {
        var y = topCenter.Y;
        var name = FSRCDText.Name(_proto, piece);
        y += Text(handle, Bold(10f, scale), name, topCenter.X, y, Fg, centered);
        if (fs.Locked.Contains(piece.ID))
            y += Text(handle, Mono(8f, scale), Loc.GetString("fs-rcd-locked"), topCenter.X, y, Bad, centered);

        var roleKey = "fs-rcd-role-" + piece.ID;
        if (piece.Mode == RcdMode.Deconstruct)
            roleKey = "fs-rcd-role-deconstruct";
        if (Loc.TryGetString(roleKey, out var role))
            y += Wrapped(handle, Body(8f, scale), role, topCenter.X, y, maxWidth, Muted, centered);

        if (_proto.TryIndex<FSBuildRecipePrototype>(piece.ID, out var recipe))
        {
            foreach (var (stack, need) in recipe.Materials)
            {
                var silo = fs.SiloStock.GetValueOrDefault(stack);
                var rest = silo > 0
                    ? Loc.GetString("fs-rcd-cost-rest-silo", ("need", need), ("name", MaterialName(stack)), ("silo", silo))
                    : Loc.GetString("fs-rcd-cost-rest", ("need", need), ("name", MaterialName(stack)));
                y += DrawCostRow(handle, stack, FSRCDText.Have(carried, fs, stack), need, rest, topCenter.X, y, scale, centered);
            }

            var meta = Loc.GetString("fs-rcd-info-time", ("seconds", piece.Delay));
            if (recipe.CapGroup is { } group && fs.CapsMax.TryGetValue(group, out var max))
                meta = $"{fs.CapsUsed.GetValueOrDefault(group)}/{max} · {meta}";
            y += Text(handle, Mono(8f, scale), meta, topCenter.X, y + 2f * scale, Muted, centered) + 2f * scale;
        }

        return y - topCenter.Y;
    }

    public float DrawCostRow(DrawingHandleScreen handle, ProtoId<StackPrototype> stack, int have, int need, string rest,
        float x, float y, float scale, bool centered)
    {
        var mono = Mono(8.5f, scale);
        var haveText = have.ToString();
        var icon = 16f * scale;
        var width = icon + 4f * scale + handle.GetDimensions(mono, haveText, 1f).X + handle.GetDimensions(mono, rest, 1f).X;
        if (centered)
            x -= width / 2f;

        if (MaterialIcon(stack) is { } tex)
            handle.DrawTextureRect(tex, UIBox2.FromDimensions(new Vector2(x, y), new Vector2(icon, icon)));
        x += icon + 4f * scale;
        var rowY = y + (icon - mono.GetLineHeight(1f)) / 2f;
        handle.DrawString(mono, new Vector2(x, rowY), haveText, have >= need ? Good : Bad);
        handle.DrawString(mono, new Vector2(x + handle.GetDimensions(mono, haveText, 1f).X, rowY), rest, Muted);
        return icon + 2f * scale;
    }

    public float MeasureInfo(DrawingHandleScreen handle, RCDPrototype piece, float scale, float maxWidth)
    {
        var height = handle.GetDimensions(Bold(10f, scale), "A", 1f).Y;
        var roleKey = piece.Mode == RcdMode.Deconstruct ? "fs-rcd-role-deconstruct" : "fs-rcd-role-" + piece.ID;
        if (Loc.TryGetString(roleKey, out var role))
            height += Wrapped(handle, Body(8f, scale), role, 0f, 0f, maxWidth, Muted, false, draw: false);

        if (_proto.TryIndex<FSBuildRecipePrototype>(piece.ID, out var recipe))
            height += recipe.Materials.Count * 18f * scale + handle.GetDimensions(Mono(8f, scale), "A", 1f).Y + 4f * scale;

        return height;
    }

    // Greedy word wrap; returns the height used. With draw false it only measures.
    public static float Wrapped(DrawingHandleScreen handle, Font font, string text, float x, float y, float maxWidth,
        Color color, bool centered, bool draw = true)
    {
        var lineHeight = font.GetLineHeight(1f);
        var line = string.Empty;
        var height = 0f;
        foreach (var word in text.Split(' '))
        {
            var attempt = line.Length == 0 ? word : string.Concat(line, " ", word);
            if (line.Length > 0 && handle.GetDimensions(font, attempt, 1f).X > maxWidth)
            {
                if (draw)
                    Text(handle, font, line, x, y + height, color, centered);
                height += lineHeight;
                line = word;
                continue;
            }

            line = attempt;
        }

        if (line.Length > 0)
        {
            if (draw)
                Text(handle, font, line, x, y + height, color, centered);
            height += lineHeight;
        }

        return height;
    }

    public static float Text(DrawingHandleScreen handle, Font font, string text, float x, float y, Color color, bool centered)
    {
        var size = handle.GetDimensions(font, text, 1f);
        handle.DrawString(font, new Vector2(centered ? x - size.X / 2f : x, y), text, color);
        return size.Y;
    }
}
