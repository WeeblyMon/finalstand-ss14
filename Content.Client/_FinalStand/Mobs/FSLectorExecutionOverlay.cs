using System.Numerics;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Player;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using static Robust.Shared.Maths.Color;

namespace Content.Client._FinalStand.Mobs;

public sealed partial class FSLectorExecutionOverlay : Overlay
{
    private const string SkullIcon = "/Textures/_FinalStand/Interface/HUD/hud_enemies.png";
    private const float SkullPx = 10f;

    private readonly IEntityManager _entManager;
    private readonly IPlayerManager _player;
    private readonly SharedTransformSystem _transform;
    private readonly MobStateSystem _mobState;
    private readonly FSMobThresholdCacheSystem _thresholdCache;
    private readonly SpriteSystem _sprite;
    private readonly DamageableSystem _damageable;
    private readonly SharedHandsSystem _handsSystem;

    private static readonly Color BorderColor = new(0.82f, 0.16f, 0.17f, 0.9f);

    // Margin for the position cull: covers the largest mob sprites, the exact bounds test comes later.
    private const float CullMargin = 2f;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public FSLectorExecutionOverlay(IEntityManager entManager, IPlayerManager player)
    {
        _entManager  = entManager;
        _player      = player;
        _transform   = entManager.System<SharedTransformSystem>();
        _mobState    = entManager.System<MobStateSystem>();
        _thresholdCache = entManager.System<FSMobThresholdCacheSystem>();
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
        var thresholdsQuery = _entManager.GetEntityQuery<MobThresholdsComponent>();
        var damageQuery = _entManager.GetEntityQuery<DamageableComponent>();
        var cullBox = args.WorldAABB.Enlarged(CullMargin);

        const float scale = 1f;
        var scaleMatrix   = Matrix3Helpers.CreateScale(new Vector2(scale, scale));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-rotation);

        var skull = FSMipmaps.PickInWorld(SkullIcon, args.Viewport, SkullPx / EyeManager.PixelsPerMeter);

        var query = _entManager.EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mobStateComp, out var xform))
        {
            if (xform.MapID != args.MapId || _mobState.IsDead(uid, mobStateComp))
                continue;

            var worldPos = _transform.GetWorldPosition(xform, xformQuery);
            if (!cullBox.Contains(worldPos))
                continue;

            if (_entManager.HasComponent<ActorComponent>(uid)
                || !thresholdsQuery.TryGetComponent(uid, out var thresholds)
                || !damageQuery.TryGetComponent(uid, out var dmg))
                continue;

            // Most of a horde on screen is healthy, so check health before the sprite bounds.
            var ratio = CalcHealthRatio(uid, mobStateComp, dmg, thresholds);
            if (ratio is not < 0.25f)
                continue;

            if (!spriteQuery.TryGetComponent(uid, out var spriteComp))
                continue;

            var bounds = _sprite.GetLocalBounds((uid, spriteComp));
            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
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

            var borderBox = new Box2(
                new Vector2(barLeft - 1f, -1f) / ppm,
                new Vector2(barRight + 1f, 4f) / ppm
            ).Translated(barOrigin);
            handle.DrawRect(borderBox, BorderColor.WithAlpha(0.35f));
            DrawBorder(handle, borderBox, BorderColor, 1f / ppm);

            if (skull != null)
            {
                var iconHalf = SkullPx / 2f / ppm;
                var iconBottom = barOrigin.Y + 4f / ppm + 2f / ppm;
                var iconBox = new Box2(-iconHalf, iconBottom, iconHalf, iconBottom + SkullPx / ppm);
                handle.DrawTextureRect(skull, iconBox);
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
        _thresholdCache.GetIncapThresholds((uid, thresholds), out var critThreshold, out var deadThreshold);

        if (_mobState.IsAlive(uid, mobStateComp))
        {
            if ((critThreshold ?? deadThreshold) is not { } threshold)
                return 1f;
            return 1f - (total / threshold).Float();
        }

        if (_mobState.IsCritical(uid, mobStateComp))
        {
            if (critThreshold is not { } crit || deadThreshold is not { } dead)
                return 0f;
            return 1f - ((total - crit) / (dead - crit)).Float();
        }

        return null;
    }
}
