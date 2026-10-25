using System.Numerics;
using Content.Client._FinalStand.Engineering;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.UI;
using Content.Client._FinalStand.WaveHud;
using Content.Shared._FinalStand.Engineering;
using Content.Shared._FinalStand.Pings;
using Content.Shared.RCD;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Pings;

public sealed partial class FSPingOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IInputManager _input = default!;

    private const float MarkerSize = 28f;
    private const float EdgeInset = 48f;
    private const float PopTime = 0.18f;
    private const float RippleTime = 0.5f;
    private const float FadeTime = 0.5f;
    private const float PanelWidth = 250f;
    private const float PanelTop = 168f;
    private const int PanelRows = 3;

    private readonly FSPingLook _look;
    private SharedTransformSystem? _xform;
    private FSPingClientSystem? _pings;
    private SpriteSystem? _sprite;
    private FSEngineeringObjectiveClientSystem? _objectives;
    private static readonly Color ObjectiveColor = FSPalette.Warn;
    private static readonly Color ScienceColor = Color.FromHex("#b48ae6");

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSPingOverlay()
    {
        IoCManager.InjectDependencies(this);
        _look = new FSPingLook(_cache);
    }

    // Where the right column's objectives and tasks end, so other panels can stack below them.
    public float ColumnBottom { get; private set; } = -1f;

    protected override void Draw(in OverlayDrawArgs args)
    {
        ColumnBottom = -1f;
        if (args.ViewportControl == null || _player.LocalEntity is not { } local)
            return;

        _xform ??= _entMan.System<SharedTransformSystem>();
        _pings ??= _entMan.System<FSPingClientSystem>();
        _sprite ??= _entMan.System<SpriteSystem>();

        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var bounds = args.ViewportBounds;
        var me = _xform.GetMapCoordinates(local);
        var now = _timing.RealTime;

        foreach (var ping in _pings.Pings)
        {
            if (!TryWorld(ping.Data, out var world) || world.MapId != me.MapId)
                continue;

            var age = (float) (now - ping.Start).TotalSeconds;
            var left = (float) (ping.End - now).TotalSeconds;
            var alpha = Math.Clamp(left / FadeTime, 0f, 1f) * (_pings.ForMe(ping.Data.Audience) ? 1f : 0.6f);
            var screen = Vector2.Transform(world.Position, matrix);
            var distance = (world.Position - me.Position).Length();

            if (Inside(bounds, screen))
                DrawInWorld(handle, ping, screen, age, alpha, distance);
            else
                DrawAtEdge(handle, ping.Data, bounds, screen, alpha, distance);
        }

        var (x, width, top) = Column(bounds);
        top = DrawObjectives(handle, x, width, top);
        ColumnBottom = DrawTasks(handle, x, width, me, top);
        DrawBossFlash(handle, bounds, now);
        DrawMuted(handle, now);
    }

    private void DrawMuted(DrawingHandleScreen handle, TimeSpan now)
    {
        var left = (_pings!.MutedUntil - now).TotalSeconds;
        if (left <= 0)
            return;

        var at = _input.MouseScreenPosition.Position + new Vector2(16f, -30f);
        if (FSMipmaps.Pick(FSPingLook.MutedIcon, 22f) is { } muted)
            handle.DrawTextureRect(muted, UIBox2.FromDimensions(at, new Vector2(22f)), FSPalette.DangerSoft);
        handle.DrawString(_look.Font(9), at + new Vector2(25f, 3f), Loc.GetString("fs-ping-muted", ("seconds", (int) Math.Ceiling(left))), FSPalette.DangerSoft);
    }

    private bool TryWorld(FSPingEvent ping, out MapCoordinates world)
    {
        if (ping.Target is { } net && _entMan.TryGetEntity(net, out var target) && _entMan.EntityExists(target))
        {
            world = _xform!.GetMapCoordinates(target.Value);
            return true;
        }

        var coords = _entMan.GetCoordinates(ping.Coords);
        if (!coords.IsValid(_entMan))
        {
            world = MapCoordinates.Nullspace;
            return false;
        }

        world = _xform!.ToMapCoordinates(coords);
        return true;
    }

    private static bool Inside(UIBox2i bounds, Vector2 p)
    {
        return p.X >= bounds.Left && p.X <= bounds.Right && p.Y >= bounds.Top && p.Y <= bounds.Bottom;
    }

    private void DrawInWorld(DrawingHandleScreen handle, FSActivePing ping, Vector2 at, float age, float alpha, float distance)
    {
        var data = ping.Data;
        var color = FSPingLook.ColorOf(data.Type);

        if (age < RippleTime)
        {
            var t = age / RippleTime;
            FSPingLook.Ring(handle, at, 6f + 34f * t, 2f, color.WithAlpha((1f - t) * alpha));
        }

        if (data.Type == FSPingType.Boss)
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(age * 6f);
            FSPingLook.Ring(handle, at, 34f + 4f * pulse, 3f, color.WithAlpha(0.8f * alpha));
        }

        if (data.BuildProto is { } build && _proto.TryIndex<RCDPrototype>(build, out var piece) && piece.Sprite is { } spec)
        {
            var ghost = _sprite!.Frame0(spec);
            handle.DrawTextureRect(ghost, UIBox2.FromDimensions(at - new Vector2(16f), new Vector2(32f)), FSPingLook.RequestColor.WithAlpha(0.55f * alpha));
        }

        var pop = age < PopTime ? Pop(age / PopTime) : 1f;
        var size = MarkerSize * pop * (data.Type == FSPingType.Boss ? 1.3f : 1f);
        var marker = at - new Vector2(0f, 30f);
        _look.DrawMarker(handle, marker, size, data.Type, alpha, data.IconPath);

        var label = _look.Font(10);
        var sub = _look.Font(8);
        var labelSize = handle.GetDimensions(label, data.Label, 1f);
        var labelAt = new Vector2(marker.X - labelSize.X / 2f, marker.Y - size / 2f - labelSize.Y - 3f);
        handle.DrawString(label, labelAt + Vector2.One, data.Label, Color.Black.WithAlpha(alpha * 0.8f));
        handle.DrawString(label, labelAt, data.Label, color.WithAlpha(alpha));

        var detail = distance > 5f ? $"{data.SenderName} · {distance:0}m" : data.SenderName;
        var detailSize = handle.GetDimensions(sub, detail, 1f);
        handle.DrawString(sub, new Vector2(marker.X - detailSize.X / 2f, marker.Y + size / 2f + 2f), detail,
            FSPingLook.InfoColor.WithAlpha(0.75f * alpha));
    }

    private void DrawAtEdge(DrawingHandleScreen handle, FSPingEvent data, UIBox2i bounds, Vector2 screen, float alpha, float distance)
    {
        var center = new Vector2((bounds.Left + bounds.Right) / 2f, (bounds.Top + bounds.Bottom) / 2f);
        var dir = screen - center;
        if (dir.LengthSquared() < 1f)
            return;

        dir = Vector2.Normalize(dir);
        var half = new Vector2((bounds.Right - bounds.Left) / 2f - EdgeInset, (bounds.Bottom - bounds.Top) / 2f - EdgeInset);
        var scale = MathF.Min(half.X / MathF.Max(MathF.Abs(dir.X), 0.001f), half.Y / MathF.Max(MathF.Abs(dir.Y), 0.001f));
        var at = center + dir * scale;

        var color = FSPingLook.ColorOf(data.Type);
        var tip = at + dir * 26f;
        var side = new Vector2(-dir.Y, dir.X) * 8f;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, new[] { tip, at + dir * 14f + side, at + dir * 14f - side }, color.WithAlpha(alpha));
        _look.DrawMarker(handle, at, 22f, data.Type, alpha, data.IconPath);

        var font = _look.Font(8);
        var text = $"{distance:0}m";
        var size = handle.GetDimensions(font, text, 1f);
        handle.DrawString(font, new Vector2(at.X - size.X / 2f, at.Y + 13f), text, FSPingLook.InfoColor.WithAlpha(alpha));
    }

    private (float X, float Width, float Top) Column(UIBox2i bounds)
    {
        if (_overlays.TryGetOverlay<WaveHudOverlay>(out var hud) && hud.PanelLeft >= 0f && hud.RightColumnBottom >= 0f)
            return (hud.PanelLeft, hud.PanelWidth, hud.RightColumnBottom + 8f);

        return (bounds.Right - PanelWidth - 24f, PanelWidth, bounds.Top + PanelTop);
    }

    // The engineer's "what's next" block: a header, then one row per job with a coloured marker, and the Mech Bay's
    // stages as pips with the next one pulsing in the colour of the department that owes it.
    private float DrawObjectives(DrawingHandleScreen handle, float x, float width, float top)
    {
        _objectives ??= _entMan.System<FSEngineeringObjectiveClientSystem>();
        if (_objectives.Latest is not { Lines.Count: > 0 } ev)
            return top;

        var header = _look.Font(7);
        var font = _look.Font(9);
        const float headerHeight = 16f;
        const float rowHeight = 18f;
        const float pipRow = 10f;
        var height = headerHeight + 6f;
        for (var i = 0; i < ev.Lines.Count; i++)
            height += rowHeight + (HasPips(ev, i) ? pipRow : 0f);
        var pulse = 0.55f + 0.45f * MathF.Sin((float) _timing.RealTime.TotalSeconds * 5f);

        FSShapes.Slanted(handle, new Vector2(x, top), width, height, 6f, FSPingLook.Back);
        FSShapes.Slanted(handle, new Vector2(x, top), 9f, height, 6f, ObjectiveColor);
        handle.DrawString(header, new Vector2(x + 14f, top + 3f), Loc.GetString("fs-objective-header"), ObjectiveColor);

        var y = top + headerHeight;
        for (var i = 0; i < ev.Lines.Count; i++)
        {
            var kind = i < ev.Kinds.Count ? ev.Kinds[i] : FSObjectiveKind.Silo;
            var science = i < ev.Science.Count && ev.Science[i];
            var color = ObjectiveTint(kind, science);
            FSShapes.Diamond(handle, new Vector2(x + 17f, y + rowHeight / 2f), 8f, color);
            handle.DrawString(font, new Vector2(x + 26f, y + 2f), Truncate(handle, font, ev.Lines[i], width - 36f),
                kind == FSObjectiveKind.BayReady ? FSPalette.Ok : FSPalette.TextBright);
            y += rowHeight;

            if (!HasPips(ev, i))
                continue;

            DrawStagePips(handle, x + 26f, y + 2f, MathF.Min(width - 36f, ev.Totals[i] * 9f), ev.Stages[i], ev.Totals[i], color, pulse);
            y += pipRow;
        }

        return top + height + 6f;
    }

    private static bool HasPips(FSEngineeringObjectiveEvent ev, int i)
    {
        return i < ev.Kinds.Count && ev.Kinds[i] == FSObjectiveKind.Bay && i < ev.Totals.Count && ev.Totals[i] > 0;
    }

    private static void DrawStagePips(DrawingHandleScreen handle, float left, float top, float width, int stage, int total, Color next, float pulse)
    {
        const float gap = 2f;
        var pip = (width - gap * (total - 1)) / total;
        for (var k = 0; k < total; k++)
        {
            var color = k < stage ? FSPalette.TextBright : k == stage ? next.WithAlpha(pulse) : Color.White.WithAlpha(0.12f);
            FSShapes.Slanted(handle, new Vector2(left + k * (pip + gap), top), pip + 2f, 5f, 2f, color);
        }
    }

    private static Color ObjectiveTint(FSObjectiveKind kind, bool science)
    {
        return kind switch
        {
            FSObjectiveKind.Bay => science ? ScienceColor : FSPalette.Money,
            FSObjectiveKind.BayReady => FSPalette.Ok,
            FSObjectiveKind.Emplacement => FSPalette.DangerSoft,
            _ => FSPalette.Warn,
        };
    }

    private float DrawTasks(DrawingHandleScreen handle, float x, float width, MapCoordinates me, float top)
    {
        var tasks = _pings!.Tasks;
        if (tasks.Count == 0)
            return top;

        var title = _look.Font(9);
        var row = _look.Font(9);
        var sub = _look.Font(8);
        var y = top;
        var shown = Math.Min(PanelRows, tasks.Count);
        var height = 22f + shown * 34f + (tasks.Count > PanelRows ? 16f : 0f);
        handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(width, height)), FSPingLook.Back);
        handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(3f, height)), FSPingLook.RequestColor);
        handle.DrawString(title, new Vector2(x + 10f, y + 4f), Loc.GetString("fs-ping-tasks-title"), FSPingLook.RequestColor);
        y += 22f;

        for (var i = 0; i < shown; i++)
        {
            var task = tasks[i];
            _look.DrawMarker(handle, new Vector2(x + 20f, y + 15f), 20f, task.Type, 1f);
            var line = Truncate(handle, row, task.Label, width - 80f);
            handle.DrawString(row, new Vector2(x + 36f, y + 2f), line, FSPingLook.InfoColor);

            var status = task.ClaimedBy is { } claimer
                ? Claimed(handle, sub, claimer, width - 90f)
                : Truncate(handle, sub, task.Requester, width - 90f);
            handle.DrawString(sub, new Vector2(x + 36f, y + 17f), status,
                task.ClaimedBy != null ? FSPingLook.EnemyColor : FSPingLook.InfoColor.WithAlpha(0.6f));

            if (TaskWorld(task) is { } world && world.MapId == me.MapId)
            {
                var offset = world.Position - me.Position;
                var distance = $"{offset.Length():0}m";
                var size = handle.GetDimensions(row, distance, 1f);
                handle.DrawString(row, new Vector2(x + width - size.X - 26f, y + 2f), distance, FSPingLook.InfoColor);
                DrawArrow(handle, new Vector2(x + width - 14f, y + 10f), offset);
            }

            y += 34f;
        }

        if (tasks.Count > PanelRows)
            handle.DrawString(sub, new Vector2(x + 10f, y), Loc.GetString("fs-ping-task-more", ("count", tasks.Count - PanelRows)), FSPingLook.InfoColor.WithAlpha(0.7f));

        return top + height + 8f;
    }

    private MapCoordinates? TaskWorld(FSTaskEntry task)
    {
        if (task.Target is { } net && _entMan.TryGetEntity(net, out var target) && _entMan.EntityExists(target))
            return _xform!.GetMapCoordinates(target.Value);

        var coords = _entMan.GetCoordinates(task.Coords);
        return coords.IsValid(_entMan) ? _xform!.ToMapCoordinates(coords) : null;
    }

    private static void DrawArrow(DrawingHandleScreen handle, Vector2 at, Vector2 worldOffset)
    {
        if (worldOffset.LengthSquared() < 0.01f)
            return;

        var dir = Vector2.Normalize(new Vector2(worldOffset.X, -worldOffset.Y));
        var side = new Vector2(-dir.Y, dir.X) * 4f;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
            new[] { at + dir * 7f, at - dir * 5f + side, at - dir * 5f - side }, FSPingLook.RequestColor);
    }

    private void DrawBossFlash(DrawingHandleScreen handle, UIBox2i bounds, TimeSpan now)
    {
        var left = (float) (_pings!.BossFlashUntil - now).TotalSeconds;
        if (left <= 0f)
            return;

        var alpha = 0.35f * Math.Clamp(left / 0.8f, 0f, 1f);
        const float band = 26f;
        var color = FSPingLook.BossColor.WithAlpha(alpha);
        handle.DrawRect(new UIBox2(bounds.Left, bounds.Top, bounds.Right, bounds.Top + band), color);
        handle.DrawRect(new UIBox2(bounds.Left, bounds.Bottom - band, bounds.Right, bounds.Bottom), color);
        handle.DrawRect(new UIBox2(bounds.Left, bounds.Top, bounds.Left + band, bounds.Bottom), color);
        handle.DrawRect(new UIBox2(bounds.Right - band, bounds.Top, bounds.Right, bounds.Bottom), color);
    }

    private static float Pop(float t)
    {
        return t < 0.7f ? 1.15f * (t / 0.7f) : 1.15f - 0.15f * ((t - 0.7f) / 0.3f);
    }

    private static string Claimed(DrawingHandleScreen handle, Font font, string name, float max)
    {
        var full = Loc.GetString("fs-ping-task-claimed", ("name", name));
        if (handle.GetDimensions(font, full, 1f).X <= max)
            return full;

        for (var len = name.Length - 1; len > 0; len--)
        {
            var cut = Loc.GetString("fs-ping-task-claimed", ("name", name.Substring(0, len) + "…"));
            if (handle.GetDimensions(font, cut, 1f).X <= max)
                return cut;
        }

        return full;
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
