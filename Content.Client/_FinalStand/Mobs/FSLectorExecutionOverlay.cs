using System.Numerics;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Robust.Shared.Player;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Utility;
using static Robust.Shared.Maths.Color;

namespace Content.Client._FinalStand.Mobs;

public sealed partial class FSLectorExecutionOverlay : Overlay
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private readonly IEntityManager _entManager;
    private readonly IPlayerManager _player;
    private readonly SharedTransformSystem _transform;
    private readonly MobStateSystem _mobState;
    private readonly MobThresholdSystem _mobThreshold;
    private readonly SpriteSystem _sprite;
    private readonly DamageableSystem _damageable;
    private readonly SharedHandsSystem _handsSystem;

    private Texture? _skullIcon;

    private static readonly Color BorderColor = new(0.82f, 0.16f, 0.17f, 0.9f);

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public FSLectorExecutionOverlay(IEntityManager entManager, IPlayerManager player)
    {
        IoCManager.InjectDependencies(this);
        _entManager  = entManager;
        _player      = player;
        _transform   = entManager.System<SharedTransformSystem>();
        _mobState    = entManager.System<MobStateSystem>();
        _mobThreshold = entManager.System<MobThresholdSystem>();
        _sprite      = entManager.System<SpriteSystem>();
        _damageable  = entManager.System<DamageableSystem>();
        _handsSystem = entManager.System<SharedHandsSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!HasExecutionUpgrade())
            return;

        var handle     = args.WorldHandle;
        var rotation   = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();
        var spriteQuery = _entManager.GetEntityQuery<SpriteComponent>();

        const float scale = 1f;
        var scaleMatrix   = Matrix3Helpers.CreateScale(new Vector2(scale, scale));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-rotation);

        _skullIcon ??= TryLoadIcon();

        var query = _entManager.EntityQueryEnumerator<DamageableComponent, MobThresholdsComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var dmg, out var thresholds, out var mobStateComp))
        {
            if (_mobState.IsDead(uid, mobStateComp))
                continue;
            if (_entManager.HasComponent<ActorComponent>(uid))
                continue;
            if (!xformQuery.TryGetComponent(uid, out var xform) || xform.MapID != args.MapId)
                continue;
            if (!spriteQuery.TryGetComponent(uid, out var spriteComp))
                continue;

            var bounds   = _sprite.GetLocalBounds((uid, spriteComp));
            var worldPos = _transform.GetWorldPosition(xform, xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var ratio = CalcHealthRatio(uid, mobStateComp, dmg, thresholds);
            if (ratio is not < 0.25f)
                continue;

            var worldMatrix  = Matrix3Helpers.CreateTranslation(worldPos);
            var scaledWorld  = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matty        = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matty);

            var ppm        = EyeManager.PixelsPerMeter;
            var yOffset    = bounds.Height * ppm / 2 - 3f;
            var widthOfMob = bounds.Width  * ppm;
            var barLeft    = 8f;
            var barRight   = widthOfMob - 8f;

            var barOrigin = new Vector2(-widthOfMob / ppm / 2, yOffset / ppm);

            // Red border around the health bar area
            var borderBox = new Box2(
                new Vector2(barLeft - 1f, -1f) / ppm,
                new Vector2(barRight + 1f, 4f) / ppm
            ).Translated(barOrigin);
            handle.DrawRect(borderBox, BorderColor.WithAlpha(0.35f));
            DrawBorder(handle, borderBox, BorderColor, 1f / ppm);

            // Skull icon centered above the bar
            if (_skullIcon != null)
            {
                const float iconPx = 10f;
                var iconHalf = iconPx / 2f / ppm;
                var iconBottom = barOrigin.Y + 4f / ppm + 2f / ppm;
                var iconBox = new Box2(-iconHalf, iconBottom, iconHalf, iconBottom + iconPx / ppm);
                handle.DrawTextureRect(_skullIcon, iconBox);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private static void DrawBorder(DrawingHandleWorld handle, Box2 box, Color color, float thickness)
    {
        handle.DrawRect(new Box2(box.Left, box.Top, box.Right, box.Top + thickness), color);
        handle.DrawRect(new Box2(box.Left, box.Bottom - thickness, box.Right, box.Bottom), color);
        handle.DrawRect(new Box2(box.Left, box.Top, box.Left + thickness, box.Bottom), color);
        handle.DrawRect(new Box2(box.Right - thickness, box.Top, box.Right, box.Bottom), color);
    }

    private bool HasExecutionUpgrade()
    {
        if (_player.LocalSession?.AttachedEntity is not { } playerEnt)
            return false;
        if (!_entManager.TryGetComponent(playerEnt, out HandsComponent? hands))
            return false;
        foreach (var handId in hands.Hands.Keys)
        {
            if (!_handsSystem.TryGetHeldItem(playerEnt, handId, out var held)) continue;
            if (_entManager.TryGetComponent(held.Value, out FSWeaponUpgradeStateComponent? state) && state.ExecutionEnabled)
                return true;
        }
        return false;
    }

    private float? CalcHealthRatio(EntityUid uid, MobStateComponent mobStateComp, DamageableComponent dmg, MobThresholdsComponent thresholds)
    {
        var total = _damageable.GetTotalDamage((uid, dmg));

        if (_mobState.IsAlive(uid, mobStateComp))
        {
            if (!_mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var threshold, thresholds) &&
                !_mobThreshold.TryGetThresholdForState(uid, MobState.Dead, out threshold, thresholds))
                return 1f;
            return 1f - ((FixedPoint2)(total / threshold)).Float();
        }

        if (_mobState.IsCritical(uid, mobStateComp))
        {
            if (!_mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var crit, thresholds) ||
                !_mobThreshold.TryGetThresholdForState(uid, MobState.Dead, out var dead, thresholds))
                return 0f;
            return 1f - ((total - crit) / (dead - crit)).Value.Float();
        }

        return null;
    }

    private Texture? TryLoadIcon()
    {
        try
        {
            return _resourceCache
                .GetResource<TextureResource>(new ResPath("/Textures/_FinalStand/Interface/HUD/hud_enemies.png"))
                .Texture;
        }
        catch { return null; }
    }
}
