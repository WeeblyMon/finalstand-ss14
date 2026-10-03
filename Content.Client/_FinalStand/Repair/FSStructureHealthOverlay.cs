using System.Numerics;
using Content.Shared._FinalStand.Departments;
using Content.Shared._FinalStand.Fortifications;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Repair;

// Engineers see a health bar under damaged fortifications; it flashes once a structure drops below 30%.
public sealed partial class FSStructureHealthOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly Color Back = new(0f, 0f, 0f, 0.75f);
    private static readonly Color Good = Color.FromHex("#7fd17a");
    private static readonly Color Worn = Color.FromHex("#d9a441");
    private static readonly Color Critical = Color.FromHex("#e0574f");
    private static readonly Color CapMark = Color.FromHex("#FF3B3B");
    private static readonly Color Progress = Color.FromHex("#5fc8e8");
    private const float Width = 34f;
    private const float Height = 5f;
    private const float CriticalFraction = 0.3f;

    private SharedTransformSystem? _xform;
    private SharedHandsSystem? _hands;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSStructureHealthOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
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
        var rivetCap = HeldRivetCap();
        var flash = 0.55f + 0.45f * MathF.Sin((float) _timing.RealTime.TotalSeconds * 8f);

        var query = _entMan.EntityQueryEnumerator<FSStructureHealthComponent, DamageableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var health, out var damageable, out var xform))
        {
            if (xform.MapID != args.MapId || health.Max <= 0f)
                continue;

            var fraction = Math.Clamp(1f - damageable.TotalDamage.Float() / health.Max, 0f, 1f);
            var progress = UpgradeProgress(uid);
            if (fraction >= 1f && progress <= 0f)
                continue;

            var color = fraction < CriticalFraction ? Critical.WithAlpha(flash) : fraction < 0.6f ? Worn : Good;
            var center = Vector2.Transform(_xform.GetWorldPosition(xform), matrix);
            var box = UIBox2.FromDimensions(center.X - Width / 2f, center.Y + 14f, Width, Height);
            handle.DrawRect(new UIBox2(box.Left - 1f, box.Top - 1f, box.Right + 1f, box.Bottom + 1f), Back);
            handle.DrawRect(new UIBox2(box.Left, box.Top, box.Left + Width * fraction, box.Bottom), color);

            if (progress > 0f)
            {
                var bar = new UIBox2(box.Left, box.Bottom + 2f, box.Right, box.Bottom + 4f);
                handle.DrawRect(new UIBox2(bar.Left - 1f, bar.Top - 1f, bar.Right + 1f, bar.Bottom + 1f), Back);
                handle.DrawRect(new UIBox2(bar.Left, bar.Top, bar.Left + Width * progress, bar.Bottom), Progress);
            }

            if (rivetCap is { } cap)
            {
                var markX = box.Left + Width * cap;
                handle.DrawRect(new UIBox2(markX, box.Top - 2f, markX + 1f, box.Bottom + 2f), CapMark);
            }
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

    private float? HeldRivetCap()
    {
        _hands ??= _entMan.System<SharedHandsSystem>();
        if (_player.LocalEntity is not { } local
            || !_hands.TryGetActiveItem(local, out var held)
            || !_entMan.HasComponent<FSRivetGunComponent>(held)
            || !_entMan.TryGetComponent<BasicEntityAmmoProviderComponent>(held, out var ammo)
            || !_proto.TryIndex<EntityPrototype>(ammo.Proto, out var proto)
            || !proto.TryGetComponent<FSRivetComponent>(out var rivet, _entMan.ComponentFactory))
            return null;

        return rivet.RepairCap;
    }
}
