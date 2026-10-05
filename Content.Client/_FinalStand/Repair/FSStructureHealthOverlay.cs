using System.Numerics;
using Content.Shared._FinalStand.Departments;
using Content.Shared._FinalStand.Fortifications;
using Content.Shared._FinalStand.HMG;
using Content.Shared._FinalStand.RCD;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Repair;

// Engineers see a health bar under damaged fortifications (flashing below 30%); everyone sees upgrade progress and nearby crate ammo.
public sealed partial class FSStructureHealthOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _cache = default!;

    private static readonly Color Back = new(0f, 0f, 0f, 0.75f);
    private static readonly Color Good = Color.FromHex("#7fd17a");
    private static readonly Color Worn = Color.FromHex("#d9a441");
    private static readonly Color Critical = Color.FromHex("#e0574f");
    private static readonly Color CapMark = Color.FromHex("#FF3B3B");
    private static readonly Color Progress = Color.FromHex("#5fc8e8");
    private const float Width = 34f;
    private const float Height = 5f;
    private const float CriticalFraction = 0.3f;
    private const float CrateRange = 2.5f;
    private static readonly Color CrateText = Color.FromHex("#e8d9a8");

    private Font? _font;

    private SharedTransformSystem? _xform;
    private SharedHandsSystem? _hands;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSStructureHealthOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _player.LocalEntity != null;
    }

    private bool IsEngineer()
    {
        return _player.LocalEntity is { } local
            && _entMan.TryGetComponent<FSDepartmentAccessComponent>(local, out var dept)
            && dept.Engineering;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        _xform ??= _entMan.System<SharedTransformSystem>();
        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var engineer = IsEngineer();
        var rivetCap = HeldRivetCap();
        var showAll = engineer && HoldingBuildTool();
        var flash = 0.55f + 0.45f * MathF.Sin((float) _timing.RealTime.TotalSeconds * 8f);

        var query = _entMan.EntityQueryEnumerator<FSStructureHealthComponent, DamageableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var health, out var damageable, out var xform))
        {
            if (xform.MapID != args.MapId || health.Max <= 0f)
                continue;

            var fraction = Math.Clamp(1f - damageable.TotalDamage.Float() / health.Max, 0f, 1f);
            var progress = UpgradeProgress(uid);
            if (!engineer)
            {
                if (progress > 0f)
                    DrawProgress(handle, Vector2.Transform(_xform.GetWorldPosition(xform), matrix), progress);
                continue;
            }

            if (fraction >= 1f && progress <= 0f && !showAll)
                continue;

            var color = fraction < CriticalFraction ? Critical.WithAlpha(flash) : fraction < 0.6f ? Worn : Good;
            if (fraction >= 1f)
                color = color.WithAlpha(0.55f);
            var center = Vector2.Transform(_xform.GetWorldPosition(xform), matrix);
            var box = UIBox2.FromDimensions(center.X - Width / 2f, center.Y + 14f, Width, Height);
            handle.DrawRect(new UIBox2(box.Left - 1f, box.Top - 1f, box.Right + 1f, box.Bottom + 1f), Back);
            handle.DrawRect(new UIBox2(box.Left, box.Top, box.Left + Width * fraction, box.Bottom), color);

            if (progress > 0f)
                DrawProgress(handle, center with { Y = center.Y + 6f }, progress);

            if (rivetCap is { } cap)
            {
                var markX = box.Left + Width * cap;
                handle.DrawRect(new UIBox2(markX, box.Top - 2f, markX + 1f, box.Bottom + 2f), CapMark);
            }
        }

        DrawCrates(handle, matrix, args.MapId, showAll);
    }

    private static void DrawProgress(DrawingHandleScreen handle, Vector2 center, float progress)
    {
        var bar = UIBox2.FromDimensions(center.X - Width / 2f, center.Y + 16f, Width, 2f);
        handle.DrawRect(new UIBox2(bar.Left - 1f, bar.Top - 1f, bar.Right + 1f, bar.Bottom + 1f), Back);
        handle.DrawRect(new UIBox2(bar.Left, bar.Top, bar.Left + Width * progress, bar.Bottom), Progress);
    }

    private void DrawCrates(DrawingHandleScreen handle, Matrix3x2 matrix, MapId map, bool showAll)
    {
        if (_player.LocalEntity is not { } local)
            return;

        _font ??= new VectorFont(_cache.GetResource<FontResource>("/Fonts/RobotoMono/RobotoMono-Bold.ttf"), 9);
        var me = _xform!.GetWorldPosition(local);
        var crates = _entMan.EntityQueryEnumerator<FSAmmoCrateComponent, TransformComponent>();
        while (crates.MoveNext(out _, out var crate, out var xform))
        {
            if (xform.MapID != map)
                continue;

            var pos = _xform.GetWorldPosition(xform);
            if (!showAll && (pos - me).Length() > CrateRange)
                continue;

            var text = crate.Rounds.ToString("N0");
            var screen = Vector2.Transform(pos, matrix);
            var size = handle.GetDimensions(_font, text, 1f);
            var at = new Vector2(screen.X - size.X / 2f, screen.Y - 30f);
            handle.DrawRect(UIBox2.FromDimensions(at - new Vector2(3f, 1f), size + new Vector2(6f, 2f)), Back);
            handle.DrawString(_font, at, text, crate.Rounds > 0 ? CrateText : Critical);
        }
    }

    private float UpgradeProgress(EntityUid uid)
    {
        if (!_entMan.TryGetComponent<FSUpgradeableComponent>(uid, out var upgradeable)
            || upgradeable.Pending is not { } pending
            || upgradeable.Poured <= 0
            || !_proto.TryIndex(pending, out var upgrade))
            return 0f;

        return (float) upgradeable.Poured / upgrade.Hits;
    }

    // With a hammer or RCD in hand every fortification shows its bar, so the whole line reads at a glance.
    private bool HoldingBuildTool()
    {
        _hands ??= _entMan.System<SharedHandsSystem>();
        return _player.LocalEntity is { } local
               && _hands.TryGetActiveItem(local, out var held)
               && (_entMan.HasComponent<FSEngineerHammerComponent>(held) || _entMan.HasComponent<FSEngineerOnlyRCDComponent>(held));
    }

    private float? HeldRivetCap()
    {
        _hands ??= _entMan.System<SharedHandsSystem>();
        if (_player.LocalEntity is not { } local
            || !_hands.TryGetActiveItem(local, out var held)
            || !_entMan.TryGetComponent<FSRivetGunComponent>(held, out var gun))
            return null;

        return gun.RepairCap;
    }
}
