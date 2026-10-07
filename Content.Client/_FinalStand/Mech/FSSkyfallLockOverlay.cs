// Missile lock reticles: red corner brackets snap in around each zombie a Skyfall missile is chasing, then hold and pulse.
using System.Numerics;
using Content.Shared._FinalStand.Mech;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Mech;

public sealed partial class FSSkyfallLockOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IResourceCache _cache = default!;

    private static readonly Color LockColor = Color.FromHex("#ff3b30");
    private static readonly ResPath FontPath = new("/Fonts/NotoSans/NotoSans-Bold.ttf");
    private const float Size = 34f;
    private const float SnapTime = 0.22f;
    private const float Corner = 0.32f;
    private const float Thickness = 2f;

    private readonly Dictionary<EntityUid, TimeSpan> _since = new();
    private readonly Dictionary<EntityUid, int> _counts = new();
    private readonly List<EntityUid> _gone = new();
    private SharedTransformSystem? _xform;
    private Font? _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSSkyfallLockOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        _xform ??= _entMan.System<SharedTransformSystem>();
        _font ??= new VectorFont(_cache.GetResource<FontResource>(FontPath), 8);
        var now = _timing.RealTime;

        _counts.Clear();
        var query = _entMan.EntityQueryEnumerator<FSSkyfallMissileComponent>();
        while (query.MoveNext(out var missile))
        {
            if (missile.LockedOn is { } target && _entMan.EntityExists(target))
                _counts[target] = _counts.GetValueOrDefault(target) + 1;
        }

        foreach (var target in _since.Keys)
        {
            if (!_counts.ContainsKey(target))
                _gone.Add(target);
        }

        foreach (var target in _gone)
            _since.Remove(target);
        _gone.Clear();

        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        foreach (var (target, count) in _counts)
        {
            var world = _xform.GetMapCoordinates(target);
            if (world.MapId != args.MapId)
                continue;

            if (!_since.TryGetValue(target, out var since))
                _since[target] = since = now;

            var t = Math.Clamp((float) (now - since).TotalSeconds / SnapTime, 0f, 1f);
            var ease = 1f - (1f - t) * (1f - t);
            var half = Size * (2f - ease) / 2f;
            var pulse = t < 1f ? 1f : 0.7f + 0.3f * MathF.Sin((float) now.TotalSeconds * 12f);
            var color = LockColor.WithAlpha(pulse);
            var center = Vector2.Transform(world.Position, matrix);
            Brackets(handle, center, half, color);

            if (t < 1f)
                continue;

            var label = count > 1 ? Loc.GetString("fs-skyfall-lock-many", ("count", count)) : Loc.GetString("fs-skyfall-lock");
            handle.DrawString(_font, center + new Vector2(half + 3f, -half - 2f), label, color);
        }
    }

    private static void Brackets(DrawingHandleScreen handle, Vector2 center, float half, Color color)
    {
        var arm = half * 2f * Corner;
        foreach (var (sx, sy) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
        {
            var corner = center + new Vector2(sx * half, sy * half);
            var x0 = sx < 0 ? corner.X : corner.X - arm;
            var y0 = sy < 0 ? corner.Y : corner.Y - Thickness;
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(x0, y0), new Vector2(arm, Thickness)), color);
            var vx = sx < 0 ? corner.X : corner.X - Thickness;
            var vy = sy < 0 ? corner.Y : corner.Y - arm;
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(vx, vy), new Vector2(Thickness, arm)), color);
        }
    }
}
