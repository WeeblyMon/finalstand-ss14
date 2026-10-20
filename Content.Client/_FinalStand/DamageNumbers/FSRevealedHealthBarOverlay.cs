using System.Numerics;
using Content.Client._FinalStand.Mobs;
using Content.Client.Overlays;
using Content.Client.UserInterface.Systems;
using Content.Shared._FinalStand.Armor;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Content.Shared.Vehicle.Components;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Content.Shared._FinalStand.Mobs;
using static Robust.Shared.Maths.Color;

namespace Content.Client._FinalStand.DamageNumbers;

public sealed class FSRevealedHealthBarOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly IOverlayManager _overlayManager;

    private readonly SharedTransformSystem _transform;
    private readonly MobStateSystem _mobStateSystem;
    private readonly FSMobThresholdCacheSystem _thresholdCache;
    private readonly SpriteSystem _spriteSystem;
    private readonly DamageableSystem _damageable;
    private readonly ProgressColorSystem _progressColor;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public readonly HashSet<EntityUid> RevealedEntities = new();

    private static readonly Color ArmorFg = new(0.2f, 0.55f, 1f, 1f);

    public FSRevealedHealthBarOverlay(IEntityManager entManager, IOverlayManager overlayManager)
    {
        _entManager = entManager;
        _overlayManager = overlayManager;
        _transform       = _entManager.System<SharedTransformSystem>();
        _mobStateSystem  = _entManager.System<MobStateSystem>();
        _thresholdCache  = _entManager.System<FSMobThresholdCacheSystem>();
        _spriteSystem    = _entManager.System<SpriteSystem>();
        _damageable      = _entManager.System<DamageableSystem>();
        _progressColor   = _entManager.System<ProgressColorSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (RevealedEntities.Count == 0)
            return;

        var handle = args.WorldHandle;
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();
        var spriteQuery = _entManager.GetEntityQuery<SpriteComponent>();

        // Containers the always-on bars already draw; those mobs only need the armor strip from here.
        _overlayManager.TryGetOverlay<EntityHealthBarOverlay>(out var healthBars);

        const float scale = 1f;
        var scaleMatrix = Matrix3Helpers.CreateScale(new Vector2(scale, scale));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-rotation);

        foreach (var uid in RevealedEntities)
        {
            if (!xformQuery.TryGetComponent(uid, out var xform) || xform.MapID != args.MapId) continue;
            if (!spriteQuery.TryGetComponent(uid, out var sprite)) continue;
            if (_entManager.HasComponent<FSNoHealthBarComponent>(uid)) continue;
            if (_entManager.HasComponent<VehicleOperatorComponent>(uid)) continue;

            if (!_entManager.TryGetComponent(uid, out FSArmorComponent? armor) || armor.MaxArmor <= 0f)
                armor = null;

            var barCovered = healthBars != null
                             && _entManager.TryGetComponent(uid, out InjurableComponent? injurable)
                             && injurable.DamageContainer is { } container
                             && healthBars.DamageContainers.Contains(container);

            if (barCovered && armor == null)
                continue;

            var bounds = _spriteSystem.GetLocalBounds((uid, sprite));
            var worldPos = _transform.GetWorldPosition(xform, xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            if (!_entManager.TryGetComponent(uid, out MobThresholdsComponent? thresholds)) continue;
            if (!_entManager.TryGetComponent(uid, out MobStateComponent? mobState)) continue;
            if (!_entManager.TryGetComponent(uid, out DamageableComponent? damageable)) continue;

            if (CalcProgress(uid, mobState, damageable, thresholds) is not { } progress)
                continue;

            var worldMatrix  = Matrix3Helpers.CreateTranslation(worldPos);
            var scaledWorld  = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matty        = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matty);

            var yOffset    = bounds.Height * EyeManager.PixelsPerMeter / 2 + 1f;
            var widthOfMob = bounds.Width  * EyeManager.PixelsPerMeter;

            var position  = new Vector2(-widthOfMob / EyeManager.PixelsPerMeter / 2,
                                         yOffset      / EyeManager.PixelsPerMeter);

            const float startX = 8f;
            var endX     = widthOfMob - 8f;

            if (!barCovered)
            {
                var color = _progressColor.GetProgressColor(progress.inCrit ? 0f : progress.ratio);
                var xFill = (endX - startX) * progress.ratio + startX;

                var bgBox = new Box2(new Vector2(startX, 0f) / EyeManager.PixelsPerMeter,
                                     new Vector2(endX,   3f) / EyeManager.PixelsPerMeter).Translated(position);
                handle.DrawRect(bgBox, Black.WithAlpha(192));

                var fillBox = new Box2(new Vector2(startX, 0f) / EyeManager.PixelsPerMeter,
                                       new Vector2(xFill,  3f) / EyeManager.PixelsPerMeter).Translated(position);
                handle.DrawRect(fillBox, color);

                var darken = new Box2(new Vector2(startX, 2f) / EyeManager.PixelsPerMeter,
                                      new Vector2(xFill,  3f) / EyeManager.PixelsPerMeter).Translated(position);
                handle.DrawRect(darken, Black.WithAlpha(128));
            }

            if (armor != null)
            {
                var armorRatio = Math.Clamp(armor.CurrentArmor / armor.MaxArmor, 0f, 1f);
                var armorFill  = (endX - startX) * armorRatio + startX;

                var armorBg = new Box2(new Vector2(startX,    -3f) / EyeManager.PixelsPerMeter,
                                       new Vector2(endX,       0f) / EyeManager.PixelsPerMeter).Translated(position);
                handle.DrawRect(armorBg, Black.WithAlpha(192));

                var armorBar = new Box2(new Vector2(startX,    -3f) / EyeManager.PixelsPerMeter,
                                        new Vector2(armorFill,  0f) / EyeManager.PixelsPerMeter).Translated(position);
                handle.DrawRect(armorBar, ArmorFg);

                var armorDarken = new Box2(new Vector2(startX,    -1f) / EyeManager.PixelsPerMeter,
                                           new Vector2(armorFill,  0f) / EyeManager.PixelsPerMeter).Translated(position);
                handle.DrawRect(armorDarken, Black.WithAlpha(128));
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private (float ratio, bool inCrit)? CalcProgress(
        EntityUid uid,
        MobStateComponent mobState,
        DamageableComponent dmg,
        MobThresholdsComponent thresholds)
    {
        var totalDamage = _damageable.GetTotalDamage((uid, dmg));
        _thresholdCache.GetIncapThresholds((uid, thresholds), out var critThreshold, out var deadThreshold);

        if (_mobStateSystem.IsAlive(uid, mobState))
        {
            if ((critThreshold ?? deadThreshold) is not { } threshold)
                return (1f, false);

            var ratio = 1f - (totalDamage / threshold).Float();
            return (ratio, false);
        }

        if (_mobStateSystem.IsCritical(uid, mobState))
        {
            if (critThreshold is not { } crit || deadThreshold is not { } dead)
                return (1f, true);

            var ratio = 1f - ((totalDamage - crit) / (dead - crit)).Float();
            return (ratio, true);
        }

        return null;
    }
}
