// The pilot HUD, drawn around the mech: hull arc on the left, power arc on the right, the weapon stack with its keys
// to the upper right, and siege or low-power callouts underneath.
using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Shared._FinalStand.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Mech;

public sealed partial class FSMechHudOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private static readonly Color Hull = FSPalette.TextBright;
    private static readonly Color Power = Color.FromHex("#4fd1e8");
    private static readonly Color Empty = Color.White.WithAlpha(0.12f);
    private static readonly Color Panel = Color.Black.WithAlpha(0.55f);
    private static readonly ResPath BoldPath = new("/Fonts/NotoSans/NotoSans-Bold.ttf");
    private const int Segments = 18;
    private const float ArcSpan = 70f;
    private const float ArcWidth = 7f;

    private FSMechHudSystem? _hud;
    private SharedTransformSystem? _xform;
    private SpriteSystem? _sprite;
    private Font? _small;
    private Font? _big;
    private float _fontScale;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSMechHudOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _hud ??= _entMan.System<FSMechHudSystem>();
        if (args.ViewportControl == null || _hud.Piloted() is not { } mech)
            return;

        _xform ??= _entMan.System<SharedTransformSystem>();
        _sprite ??= _entMan.System<SpriteSystem>();
        var scale = _ui.DefaultUIScale;
        if (_small == null || Math.Abs(_fontScale - scale) > 0.01f)
        {
            _fontScale = scale;
            var font = _cache.GetResource<FontResource>(BoldPath);
            _small = new VectorFont(font, (int) (8 * scale));
            _big = new VectorFont(font, (int) (13 * scale));
        }

        var world = _xform.GetMapCoordinates(mech);
        if (world.MapId != args.MapId)
            return;

        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var center = Vector2.Transform(world.Position, matrix);
        var tile = (Vector2.Transform(world.Position + Vector2.UnitX, matrix) - center).Length();
        var radius = tile * 1.55f;
        var handle = args.ScreenHandle;
        var comp = mech.Comp;

        var hull = comp.MaxIntegrity > 0 ? (comp.Integrity / comp.MaxIntegrity).Float() : 0f;
        var power = comp.MaxEnergy > 0 ? (comp.Energy / comp.MaxEnergy).Float() : 0f;
        var hullColor = hull < 0.25f ? FSPalette.Danger : hull < 0.5f ? FSPalette.Warn : Hull;
        var powerColor = power < 0.25f ? FSPalette.Danger : Power;

        Arc(handle, center, radius, 180f + ArcSpan / 2f, -1f, hull, hullColor, scale);
        Arc(handle, center, radius, ArcSpan / 2f, 1f, power, powerColor, scale);

        var low = center + new Vector2(0f, radius);
        Label(handle, _small!, Loc.GetString("fs-mech-hud-hull", ("value", (int) MathF.Round(hull * 100))), low + new Vector2(-radius * 0.95f, 0f), hullColor, right: true);
        Label(handle, _small!, Loc.GetString("fs-mech-hud-power", ("value", (int) MathF.Round(power * 100))), low + new Vector2(radius * 0.95f, 0f), powerColor, right: false);

        DrawWeapons(handle, comp, center + new Vector2(radius + 22f * scale, -radius * 0.85f), scale);

        var under = center + new Vector2(0f, radius + 18f * scale);
        if (_entMan.TryGetComponent<FSSiegeModeComponent>(mech, out var siege) && siege.Planted)
        {
            Centered(handle, _big!, Loc.GetString("fs-mech-hud-siege"), under, Power);
            under += new Vector2(0f, 18f * scale);
        }

        if (power < 0.3f && (int) (_timing.RealTime.TotalSeconds * 2) % 2 == 0)
            Centered(handle, _small!, Loc.GetString("fs-mech-hud-low-power"), under, FSPalette.Danger);
    }

    private static void Arc(DrawingHandleScreen handle, Vector2 center, float radius, float bottomDeg, float direction,
        float fraction, Color color, float scale)
    {
        var lit = (int) MathF.Ceiling(Math.Clamp(fraction, 0f, 1f) * Segments);
        var step = ArcSpan / Segments;
        var width = ArcWidth * scale;
        for (var i = 0; i < Segments; i++)
        {
            var a0 = MathHelper.DegreesToRadians(bottomDeg - direction * (i * step + 0.6f));
            var a1 = MathHelper.DegreesToRadians(bottomDeg - direction * ((i + 1) * step - 0.6f));
            var d0 = new Vector2(MathF.Cos((float) a0), MathF.Sin((float) a0));
            var d1 = new Vector2(MathF.Cos((float) a1), MathF.Sin((float) a1));
            var outer = radius + width * (i == lit - 1 ? 1.35f : 1f);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, new[]
            {
                center + d0 * radius, center + d0 * outer, center + d1 * outer, center + d1 * radius,
            }, Color.ToSrgb(i < lit ? color : Empty));
        }
    }

    private void DrawWeapons(DrawingHandleScreen handle, MechComponent mech, Vector2 topLeft, float scale)
    {
        var row = 34f * scale;
        var width = 118f * scale;
        var icon = 28f * scale;
        var equipment = mech.EquipmentContainer.ContainedEntities;
        for (var i = 0; i < equipment.Count && i < FSMechHudSystem.WeaponKeys; i++)
        {
            var item = equipment[i];
            var selected = mech.CurrentSelectedEquipment == item;
            var at = topLeft + new Vector2(selected ? -6f * scale : 0f, i * (row + 4f * scale));
            var box = UIBox2.FromDimensions(at, new Vector2(width, row));
            handle.DrawRect(box, Panel);
            handle.DrawRect(box, selected ? Power : Color.White.WithAlpha(0.18f), false);
            if (selected)
                handle.DrawRect(UIBox2.FromDimensions(at, new Vector2(3f * scale, row)), Power);

            var key = UIBox2.FromDimensions(at + new Vector2(6f * scale, 4f * scale), new Vector2(14f * scale, 14f * scale));
            handle.DrawRect(key, selected ? Power : Color.White.WithAlpha(0.35f), false);
            Centered(handle, _small!, (i + 1).ToString(), key.Center - new Vector2(0f, 5f * scale), selected ? Power : FSPalette.TextMuted);

            if (_entMan.GetComponentOrNull<MetaDataComponent>(item)?.EntityPrototype is { } proto)
            {
                var texture = _sprite!.GetPrototypeIcon(proto.ID).Default;
                handle.DrawTextureRect(texture, UIBox2.FromDimensions(at + new Vector2(24f * scale, (row - icon) / 2f), new Vector2(icon)),
                    selected ? Color.White : Color.White.WithAlpha(0.55f));
            }

            var ammo = new GetAmmoCountEvent();
            _entMan.EventBus.RaiseLocalEvent(item, ref ammo);
            var text = ammo.Capacity > 0 ? ammo.Count.ToString() : "-";
            var size = handle.GetDimensions(_big!, text, 1f);
            handle.DrawString(_big!, at + new Vector2(width - size.X - 8f * scale, (row - size.Y) / 2f), text,
                ammo.Capacity > 0 && ammo.Count == 0 ? FSPalette.Danger : selected ? FSPalette.TextBright : FSPalette.TextMuted);
        }
    }

    private static void Label(DrawingHandleScreen handle, Font font, string text, Vector2 at, Color color, bool right)
    {
        var size = handle.GetDimensions(font, text, 1f);
        handle.DrawString(font, new Vector2(right ? at.X - size.X : at.X, at.Y), text, color);
    }

    private static void Centered(DrawingHandleScreen handle, Font font, string text, Vector2 at, Color color)
    {
        var size = handle.GetDimensions(font, text, 1f);
        handle.DrawString(font, at - new Vector2(size.X / 2f, 0f), text, color);
    }
}
