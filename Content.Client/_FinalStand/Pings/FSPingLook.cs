using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Pings;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client._FinalStand.Pings;

// Shape follows the category so colour-blind players can tell pings apart.
public enum FSPingShape : byte
{
    Diamond,
    Circle,
    Square,
}

public enum FSPingCategory : byte
{
    Boss,
    Enemy,
    Request,
    Info,
}

public sealed class FSPingLook
{
    public static readonly Color EnemyColor = Color.FromHex("#f08a24");
    public static readonly Color BossColor = Color.FromHex("#e0303a");
    public static readonly Color RequestColor = Color.FromHex("#4fa3ff");
    public static readonly Color InfoColor = FSPalette.TextBright;
    public static readonly Color Back = FSPalette.PanelDeep;

    private const string IconDir = "/Textures/_FinalStand/Interface/Pings/";
    public const string MutedIcon = IconDir + "muted.png";

    private readonly IResourceCache _cache;
    private readonly Dictionary<int, Font> _fonts = new();

    public FSPingLook(IResourceCache cache)
    {
        _cache = cache;
    }

    public Font Font(int size)
    {
        if (!_fonts.TryGetValue(size, out var font))
            _fonts[size] = font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), size);
        return font;
    }

    public static string IconName(FSPingType type) => type switch
    {
        FSPingType.Go => "go",
        FSPingType.Danger => "danger",
        FSPingType.Enemy or FSPingType.Special => "enemy",
        FSPingType.Boss => "boss",
        FSPingType.Defend => "defend",
        FSPingType.FallBack => "fallback",
        FSPingType.NeedAmmo or FSPingType.Ammo => "ammo",
        FSPingType.NeedChems => "chems",
        FSPingType.NeedRepair or FSPingType.Repair => "repair",
        FSPingType.BuildHere => "build",
        FSPingType.Revive => "revive",
        FSPingType.Loot => "loot",
        FSPingType.Bay => "bay",
        FSPingType.Door => "door",
        _ => "ack",
    };

    public static FSPingCategory CategoryOf(FSPingType type) => type switch
    {
        FSPingType.Boss => FSPingCategory.Boss,
        FSPingType.Enemy or FSPingType.Special or FSPingType.Danger => FSPingCategory.Enemy,
        FSPingType.NeedAmmo or FSPingType.NeedChems or FSPingType.NeedRepair or FSPingType.Repair
            or FSPingType.BuildHere or FSPingType.Revive or FSPingType.Bay => FSPingCategory.Request,
        _ => FSPingCategory.Info,
    };

    public static Color ColorOf(FSPingType type) => CategoryOf(type) switch
    {
        FSPingCategory.Boss => BossColor,
        FSPingCategory.Enemy => EnemyColor,
        FSPingCategory.Request => RequestColor,
        _ => InfoColor,
    };

    public static FSPingShape ShapeOf(FSPingType type) => CategoryOf(type) switch
    {
        FSPingCategory.Boss or FSPingCategory.Enemy => FSPingShape.Diamond,
        FSPingCategory.Request => FSPingShape.Circle,
        _ => FSPingShape.Square,
    };

    // A filled marker in the category shape with a darker rim, then the icon tinted dark on top.
    public void DrawMarker(DrawingHandleScreen handle, Vector2 center, float size, FSPingType type, float alpha, string? iconPath = null)
    {
        var color = ColorOf(type);
        Shape(handle, center, size / 2f + 2f, ShapeOf(type), Back.WithAlpha(alpha * 0.9f));
        Shape(handle, center, size / 2f, ShapeOf(type), color.WithAlpha(alpha));
        var side = size * (iconPath != null ? 0.8f : 0.62f);
        var tint = iconPath != null ? Color.White.WithAlpha(alpha) : Back.WithAlpha(alpha);
        var path = iconPath is { Length: > 0 } custom ? custom : IconDir + IconName(type) + ".png";
        if (FSMipmaps.Pick(path, side) is { } icon)
            handle.DrawTextureRect(icon, UIBox2.FromDimensions(center - new Vector2(side / 2f), new Vector2(side)), tint);
    }

    public static void Shape(DrawingHandleScreen handle, Vector2 center, float r, FSPingShape shape, Color color)
    {
        switch (shape)
        {
            case FSPingShape.Circle:
                FSShapes.Disc(handle, center, r, color);
                break;
            case FSPingShape.Diamond:
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan,
                    new[] { center + new Vector2(0, -r), center + new Vector2(r, 0), center + new Vector2(0, r), center + new Vector2(-r, 0) },
                    color);
                break;
            default:
                var s = r * 0.86f;
                handle.DrawRect(new UIBox2(center.X - s, center.Y - s, center.X + s, center.Y + s), color);
                break;
        }
    }

    public static void Ring(DrawingHandleScreen handle, Vector2 center, float r, float width, Color color)
    {
        FSShapes.Ring(handle, center, r, width, color);
    }
}
