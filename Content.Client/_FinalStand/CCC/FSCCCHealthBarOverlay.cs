using System.Numerics;
using Content.Client.UserInterface.Systems;
using Content.Shared._FinalStand.CCC;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using static Robust.Shared.Maths.Color;

namespace Content.Client._FinalStand.CCC;

public sealed class FSCCCHealthBarOverlay : Overlay
{
    private readonly IEntityManager _entMan;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _spriteSystem;
    private readonly DamageableSystem _damageable;
    private readonly ProgressColorSystem _progressColor;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public FSCCCHealthBarOverlay(IEntityManager entMan)
    {
        _entMan = entMan;
        _transform     = _entMan.System<SharedTransformSystem>();
        _spriteSystem  = _entMan.System<SpriteSystem>();
        _damageable    = _entMan.System<DamageableSystem>();
        _progressColor = _entMan.System<ProgressColorSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var rotMatrix = Matrix3Helpers.CreateRotation(-rotation);
        var xformQuery = _entMan.GetEntityQuery<TransformComponent>();
        var spriteQuery = _entMan.GetEntityQuery<SpriteComponent>();

        var query = _entMan.AllEntityQueryEnumerator<FinalStandCCCTagComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var tag, out var dmg))
        {
            if (tag.MaxHealth <= 0f) continue;
            if (!xformQuery.TryGetComponent(uid, out var xform) || xform.MapID != args.MapId) continue;
            if (!spriteQuery.TryGetComponent(uid, out var sprite)) continue;

            var bounds = _spriteSystem.GetLocalBounds((uid, sprite));
            var worldPos = _transform.GetWorldPosition(xform, xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB)) continue;

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPos);
            var matty = Matrix3x2.Multiply(rotMatrix, worldMatrix);
            handle.SetTransform(matty);

            var totalDamage = _damageable.GetTotalDamage((uid, dmg)).Float();
            var ratio = Math.Clamp(1f - totalDamage / tag.MaxHealth, 0f, 1f);

            var ppm = EyeManager.PixelsPerMeter;
            var widthOfMob = bounds.Width * ppm;
            // bar sits just below the sprite bottom
            var yOffset = -(bounds.Height * ppm / 2f + 5f);
            var pos = new Vector2(-widthOfMob / ppm / 2f, yOffset / ppm);

            var endX = widthOfMob;
            var xFill = endX * ratio;

            var bgBox = new Box2(new Vector2(0f, 0f) / ppm,
                                 new Vector2(endX, 4f) / ppm).Translated(pos);
            handle.DrawRect(bgBox, Black.WithAlpha(200));

            var fillBox = new Box2(new Vector2(0f, 0f) / ppm,
                                   new Vector2(xFill, 4f) / ppm).Translated(pos);
            handle.DrawRect(fillBox, _progressColor.GetProgressColor(ratio));

            var darken = new Box2(new Vector2(0f, 3f) / ppm,
                                  new Vector2(xFill, 4f) / ppm).Translated(pos);
            handle.DrawRect(darken, Black.WithAlpha(128));
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
