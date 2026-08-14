using System.Numerics;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Weapons;

public sealed partial class FSRadiationMarkOverlay : Overlay
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private readonly IEntityManager _entManager;
    private readonly SharedTransformSystem _transform;
    private readonly MobStateSystem _mobState;
    private readonly SpriteSystem _sprite;

    private Texture? _icon;

    private static readonly Color IconColor = new(0.4f, 1.0f, 0.1f, 0.9f);

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public FSRadiationMarkOverlay(IEntityManager entManager)
    {
        IoCManager.InjectDependencies(this);
        _entManager = entManager;
        _transform  = entManager.System<SharedTransformSystem>();
        _mobState   = entManager.System<MobStateSystem>();
        _sprite     = entManager.System<SpriteSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle         = args.WorldHandle;
        var rotation       = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var xformQuery     = _entManager.GetEntityQuery<TransformComponent>();
        var spriteQuery    = _entManager.GetEntityQuery<SpriteComponent>();
        var scaleMatrix    = Matrix3Helpers.CreateScale(Vector2.One);
        var rotationMatrix = Matrix3Helpers.CreateRotation(-rotation);

        _icon ??= TryLoadIcon();
        if (_icon == null)
            return;

        var query = _entManager.EntityQueryEnumerator<FSRadiationMarkComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var mobStateComp))
        {
            if (_mobState.IsDead(uid, mobStateComp))
                continue;
            if (!xformQuery.TryGetComponent(uid, out var xform) || xform.MapID != args.MapId)
                continue;
            if (!spriteQuery.TryGetComponent(uid, out var spriteComp))
                continue;

            var bounds   = _sprite.GetLocalBounds((uid, spriteComp));
            var worldPos = _transform.GetWorldPosition(xform, xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matty       = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matty);

            var ppm        = EyeManager.PixelsPerMeter;
            const float iconPx = 20f;
            var iconHalf   = iconPx / 2f / ppm;
            var iconBottom = bounds.Height / 2f;
            var iconBox    = new Box2(-iconHalf, iconBottom, iconHalf, iconBottom + iconPx / ppm);
            handle.DrawTextureRect(_icon, iconBox, IconColor);
        }

        handle.SetTransform(Matrix3x2.Identity);
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
