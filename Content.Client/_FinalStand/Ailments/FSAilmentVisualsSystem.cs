using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Shared._FinalStand.Ailments;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Ailments;

public sealed partial class FSAilmentVisualsSystem : EntitySystem
{
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;

    public static readonly FSAilmentSlot[] Slots = Enum.GetValues<FSAilmentSlot>();

    private static readonly TimeSpan OverlaySyncInterval = TimeSpan.FromSeconds(0.25);
    private static readonly Dictionary<FSAilmentSlot, string> OverlayKeys = new();

    private readonly Dictionary<FSAilmentSlot, SpriteSpecifier> _overlays = new();
    private readonly Dictionary<(EntityUid Patient, FSAilmentSlot Slot), SpriteSpecifier> _shown = new();
    private readonly List<(EntityUid Patient, FSAilmentSlot Slot)> _stale = new();
    private TimeSpan _nextOverlaySync;

    static FSAilmentVisualsSystem()
    {
        foreach (var slot in Slots)
            OverlayKeys[slot] = $"fs-ailment-{slot}";
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_timing.CurTime < _nextOverlaySync)
            return;

        _nextOverlaySync = _timing.CurTime + OverlaySyncInterval;
        SyncOverlays();
    }

    private void SyncOverlays()
    {
        _stale.Clear();
        foreach (var (key, _) in _shown)
        {
            if (TerminatingOrDeleted(key.Patient))
                _stale.Add(key);
        }

        foreach (var key in _stale)
            _shown.Remove(key);

        var query = EntityQueryEnumerator<FSAilmentStateComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
            _ailments.GetOverlays(uid, _overlays);
            foreach (var slot in Slots)
            {
                var key = (uid, slot);
                var wanted = _overlays.GetValueOrDefault(slot);
                var shown = _shown.GetValueOrDefault(key);
                if (Equals(wanted, shown))
                    continue;

                if (wanted != null)
                {
                    var layer = _sprite.LayerMapReserve((uid, sprite), OverlayKeys[slot]);
                    _sprite.LayerSetSprite((uid, sprite), layer, wanted);
                    _sprite.LayerSetVisible((uid, sprite), layer, true);
                    _shown[key] = wanted;
                }
                else
                {
                    if (_sprite.LayerMapTryGet((uid, sprite), OverlayKeys[slot], out var layer, false))
                        _sprite.LayerSetVisible((uid, sprite), layer, false);

                    _shown.Remove(key);
                }
            }
        }
    }

    public static Color SlotColor(FSAilmentSlot slot) => slot switch
    {
        FSAilmentSlot.Bleeding => FSPalette.Danger,
        FSAilmentSlot.Bone => FSPalette.TextBright,
        FSAilmentSlot.Burn => FSPalette.Burn,
        FSAilmentSlot.Bloater => FSPalette.Toxic,
        FSAilmentSlot.Heart => FSPalette.Pulse,
        _ => FSPalette.TextMuted,
    };

    public static bool Has(FSAilmentPips pips, FSAilmentSlot slot)
    {
        return (pips & (FSAilmentPips) (1 << (int) slot)) != 0;
    }

    public static int Count(FSAilmentPips pips)
    {
        var count = 0;
        foreach (var slot in Slots)
        {
            if (Has(pips, slot))
                count++;
        }

        return count;
    }

    public static void DrawArc(DrawingHandleWorld handle, Vector2 centre, float radius, float fraction, Color color)
    {
        const int segments = 40;
        const float thickness = 0.045f;

        handle.DrawCircle(centre, radius, Color.Black.WithAlpha(0.45f), false);

        var end = segments * Math.Clamp(fraction, 0f, 1f);
        for (var i = 0; i < end; i++)
        {
            var a0 = MathF.PI / 2f - MathF.Tau * i / segments;
            var a1 = MathF.PI / 2f - MathF.Tau * MathF.Min(i + 1, end) / segments;
            for (var r = 0f; r < thickness; r += 0.015f)
            {
                handle.DrawLine(centre + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * (radius - r),
                    centre + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * (radius - r), color);
            }
        }
    }

    public Texture? Pip(FSAilmentSlot slot)
    {
        return _ailments.Rules.Slots.TryGetValue(slot, out var info) && info.Pip is { } pip ? _sprite.Frame0(pip) : null;
    }

    public Texture? FixIcon(FSAilmentView view)
    {
        return view.FixIcon is { } icon ? _sprite.Frame0(icon) : FixIcon(view.Slot);
    }

    public Texture? FixIcon(FSAilmentSlot slot)
    {
        return _ailments.Rules.Slots.TryGetValue(slot, out var info) && info.FixIcon is { } icon ? _sprite.Frame0(icon) : null;
    }

    public float DrawWorldPips(DrawingHandleWorld handle, Vector2 origin, FSAilmentPips pips, float size)
    {
        var x = origin.X;
        foreach (var slot in Slots)
        {
            if (!Has(pips, slot) || Pip(slot) is not { } texture)
                continue;

            handle.DrawTextureRect(texture, new Box2(x, origin.Y, x + size, origin.Y + size), SlotColor(slot));
            x += size;
        }

        return x - origin.X;
    }

    public float DrawScreenPips(DrawingHandleScreen handle, Vector2 origin, FSAilmentPips pips, float size)
    {
        var x = origin.X;
        foreach (var slot in Slots)
        {
            if (!Has(pips, slot) || Pip(slot) is not { } texture)
                continue;

            handle.DrawTextureRect(texture, new UIBox2(x, origin.Y, x + size, origin.Y + size), SlotColor(slot));
            x += size + 1f;
        }

        return x - origin.X;
    }
}
