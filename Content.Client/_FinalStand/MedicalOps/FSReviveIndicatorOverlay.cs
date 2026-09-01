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
    private readonly MobStateSystem _mobState;

    private readonly Texture? _icon;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    // Width in metres. Height follows the texture's own aspect, or the badge comes out squashed.
    private const float Width = 0.95f;

    // Measured from the body's own origin, not from the top of its bounds, so the badge sits over
    // the casualty rather than trailing above whatever the health bar happens to be doing.
    private const float VerticalOffset = 0.12f;

    private const float BobAmplitude = 0.05f;
    private const float BobSpeed = 2.6f;

    public FSReviveIndicatorOverlay(IEntityManager entManager, IGameTiming timing, IResourceCache cache)
    {
        _entManager = entManager;
        _timing = timing;
        _transform = _entManager.System<SharedTransformSystem>();
        _mobState = _entManager.System<MobStateSystem>();

        // Overlays sharing a space draw in arbitrary order otherwise, so the badge would flicker
        // behind the health bar it is meant to sit on top of.
        ZIndex = 10;

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

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPos);
            handle.SetTransform(Matrix3x2.Multiply(rotationMatrix, worldMatrix));

            // Slow bob so it reads as asking for something rather than being scenery.
            var bob = MathF.Sin(time * BobSpeed) * BobAmplitude;
            var halfWidth = Width * 0.5f;
            var halfHeight = Width * ((float)_icon.Height / _icon.Width) * 0.5f;
            var centre = VerticalOffset + bob;

            handle.DrawTextureRect(_icon,
                new Box2(-halfWidth, centre - halfHeight, halfWidth, centre + halfHeight));
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
