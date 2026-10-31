using System.Numerics;
using Content.Client._FinalStand.Ailments;
using Content.Client._FinalStand.Interface;
using Content.Shared._FinalStand.Ailments;
using Content.Shared.Mobs;
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

public sealed class FSReviveIndicatorOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly MobStateSystem _mobState;
    private readonly MobThresholdSystem _thresholds;
    private readonly FSAilmentSystem _ailments;

    private readonly Texture? _iconDead;
    private readonly Texture? _iconCrit;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private const float Width = 0.95f;

    private const float VerticalOffset = 0.42f;

    private const float BobAmplitude = 0.05f;
    private const float BobSpeed = 2.6f;

    private const float RingRadius = 0.56f;

    public FSReviveIndicatorOverlay(IEntityManager entManager, IGameTiming timing, IResourceCache cache)
    {
        _entManager = entManager;
        _timing = timing;
        _transform = _entManager.System<SharedTransformSystem>();
        _mobState = _entManager.System<MobStateSystem>();
        _thresholds = _entManager.System<MobThresholdSystem>();
        _ailments = _entManager.System<FSAilmentSystem>();

        ZIndex = 10;

        _iconDead = FSOverlayTextures.TryLoad(cache, "/Textures/_FinalStand/Interface/MedicPing/revive_dead.png");
        _iconCrit = FSOverlayTextures.TryLoad(cache, "/Textures/_FinalStand/Interface/MedicPing/revive_crit.png");
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_iconDead == null && _iconCrit == null)
            return;

        var handle = args.WorldHandle;
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var rotationMatrix = Matrix3Helpers.CreateRotation(-rotation);
        var time = (float)_timing.RealTime.TotalSeconds;

        var query = _entManager.EntityQueryEnumerator<MobStateComponent, MindContainerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var mindContainer, out var xform))
        {
            if (!mindContainer.HasMind || xform.MapID != args.MapId)
                continue;

            if (!_mobState.IsIncapacitated(uid, mobState))
                continue;

            var icon = _mobState.IsDead(uid, mobState) ? _iconDead : _iconCrit;
            icon ??= _iconDead ?? _iconCrit;
            if (icon == null)
                continue;

            var worldPos = _transform.GetWorldPosition(xform);
            if (!args.WorldAABB.Enlarged(1f).Contains(worldPos))
                continue;

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPos);
            handle.SetTransform(Matrix3x2.Multiply(rotationMatrix, worldMatrix));

            var bob = MathF.Sin(time * BobSpeed) * BobAmplitude;
            var halfWidth = Width * 0.5f;
            var halfHeight = Width * ((float)icon.Height / icon.Width) * 0.5f;
            var centre = VerticalOffset + bob;

            handle.DrawTextureRect(icon,
                new Box2(-halfWidth, centre - halfHeight, halfWidth, centre + halfHeight));

            if (_mobState.IsCritical(uid, mobState) && _entManager.TryGetComponent(uid, out FSAilmentStateComponent? vitals))
                DrawClock(handle, new Vector2(0f, centre), uid, vitals);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawClock(DrawingHandleWorld handle, Vector2 centre, EntityUid uid, FSAilmentStateComponent vitals)
    {
        float fraction;
        Color color;
        if (vitals.Held)
        {
            fraction = 1f;
            color = FSPalette.Ok;
        }
        else if (vitals.DownEnd is { } end)
        {
            var full = FullClock(uid);
            fraction = full > 0f ? Math.Clamp((float) (end - _timing.CurTime).TotalSeconds / full, 0f, 1f) : 1f;
            color = FSPalette.Danger;
        }
        else
        {
            return;
        }

        FSAilmentVisualsSystem.DrawArc(handle, centre, RingRadius, fraction, color);
    }

    private float FullClock(EntityUid uid)
    {
        var drain = _ailments.Rules.DownDrain.GetTotal().Float();
        if (drain <= 0f
            || !_thresholds.TryGetThresholdForState(uid, MobState.Critical, out var crit)
            || !_thresholds.TryGetThresholdForState(uid, MobState.Dead, out var dead))
        {
            return 0f;
        }

        return (dead.Value - crit.Value).Float() / drain;
    }
}
