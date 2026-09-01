using System.Numerics;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mind.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.MedicalOps;

// A downed player is only worth rescuing if someone can tell they are down, so the marker is drawn
// for everyone rather than gated to medical.
public sealed class FSReviveIndicatorOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _spriteSystem;
    private readonly MobStateSystem _mobState;

    private readonly Texture? _icon;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private const float Size = 0.55f;
    private const float BobAmplitude = 0.05f;
    private const float BobSpeed = 2.6f;

    public FSReviveIndicatorOverlay(IEntityManager entManager, IGameTiming timing, IResourceCache cache)
    {
        _entManager = entManager;
        _timing = timing;
        _transform = _entManager.System<SharedTransformSystem>();
        _spriteSystem = _entManager.System<SpriteSystem>();
        _mobState = _entManager.System<MobStateSystem>();

        try
        {
            _icon = cache.GetResource<TextureResource>(
                new ResPath("/Textures/_FinalStand/Interface/MedicPing/revive.png")).Texture;
        }
        catch
        {
            _icon = null;
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_icon == null)
            return;

        var handle = args.WorldHandle;
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var rotationMatrix = Matrix3Helpers.CreateRotation(-rotation);
        var time = (float)_timing.RealTime.TotalSeconds;

        var query = _entManager.EntityQueryEnumerator<MobStateComponent, MindContainerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var mindContainer, out var xform))
        {
            // Players only. A downed zombie is not a rescue.
            if (!mindContainer.HasMind || xform.MapID != args.MapId)
                continue;

            if (!_mobState.IsIncapacitated(uid))
                continue;

            var worldPos = _transform.GetWorldPosition(xform);
            if (!args.WorldAABB.Enlarged(1f).Contains(worldPos))
                continue;

            var height = 1f;
            if (_entManager.TryGetComponent(uid, out SpriteComponent? sprite))
                height = _spriteSystem.GetLocalBounds((uid, sprite)).Height;

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPos);
            handle.SetTransform(Matrix3x2.Multiply(rotationMatrix, worldMatrix));

            // Sits above the health bar, with a slow bob so it reads as asking for something.
            var bob = MathF.Sin(time * BobSpeed) * BobAmplitude;
            var half = Size * 0.5f;
            var centre = height / 2f + half + 0.25f + bob;

            handle.DrawTextureRect(_icon, new Box2(-half, centre - half, half, centre + half));
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
