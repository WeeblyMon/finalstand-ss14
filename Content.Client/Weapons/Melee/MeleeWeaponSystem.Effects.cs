using System.Numerics;
using Content.Client._FinalStand.Melee;
using Content.Shared._FinalStand.Melee;
using Content.Client.Animations;
using Content.Client.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Map;

namespace Content.Client.Weapons.Melee;

public sealed partial class MeleeWeaponSystem
{
    private const string FadeAnimationKey = "melee-fade";
    private const string SlashAnimationKey = "melee-slash";
    private const string ThrustAnimationKey = "melee-thrust";

    /// <summary>
    /// Does all of the melee effects for a player that are predicted, i.e. character lunge and weapon animation.
    /// </summary>
    public override void DoLunge(EntityUid user, EntityUid weapon, Angle angle, Vector2 localPos, string? animation, bool predicted = true)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        // FINALSTAND: per-weapon anticipation, impact hold, reach, spin and trail
        var juiceHeavy = animation != null && TryComp(weapon, out MeleeWeaponComponent? juiceMelee) && animation == juiceMelee.WideAnimation && animation != juiceMelee.Animation;
        var juice = _fsJuice.GetProfile(user, weapon, juiceHeavy);
        var anticipation = juice?.Anticipation ?? 0f;
        var hold = _fsJuice.TakeHold(user);

        var lunge = GetLungeAnimation(localPos, anticipation, hold);

        // Stop any existing lunges on the user.
        _animation.Stop(user, MeleeLungeKey);
        _animation.Play(user, lunge, MeleeLungeKey);

        if (localPos == Vector2.Zero || animation == null)
            return;

        if (!TryComp(user, out TransformComponent? userXform) || userXform.MapID == MapId.Nullspace)
            return;

        var animationUid = Spawn(animation, userXform.Coordinates);

        if (!TryComp<SpriteComponent>(animationUid, out var sprite)
            || !TryComp<WeaponArcVisualsComponent>(animationUid, out var arcComponent))
        {
            return;
        }

        var length = 1f;
        var offset = 1f;

        var spriteRotation = Angle.Zero;
        if (arcComponent.Animation != WeaponArcAnimation.None
            && TryComp(weapon, out MeleeWeaponComponent? meleeWeaponComponent))
        {
            if (user != weapon
                && TryComp(weapon, out SpriteComponent? weaponSpriteComponent))
                _sprite.CopySprite((weapon, weaponSpriteComponent), (animationUid, sprite));

            spriteRotation = meleeWeaponComponent.WideAnimationRotation;

            if (meleeWeaponComponent.SwingLeft)
                angle *= -1;

            length = (1 / meleeWeaponComponent.AttackRate) * 0.6f;
            offset = meleeWeaponComponent.AnimationOffset * (juice?.Reach ?? 1f); // FINALSTAND
        }
        _sprite.SetRotation((animationUid, sprite), localPos.ToWorldAngle());

        var xform = Transform(animationUid);
        TrackUserComponent track;

        switch (arcComponent.Animation)
        {
            case WeaponArcAnimation.Slash:
                track = EnsureComp<TrackUserComponent>(animationUid);
                track.User = user;
                // FINALSTAND: full-circle wide attacks spin and overhead ones slam instead of slashing
                var style = _fsJuice.GetStyle(weapon, juiceHeavy);
                var slash = style == FSMeleeHeavyStyle.Spin && Math.Abs(angle.Theta) >= Math.Tau - 0.01
                    ? _fsJuice.GetSpinAnimation((animationUid, sprite), spriteRotation, length, offset, anticipation, hold, angle.Theta < 0)
                    : style == FSMeleeHeavyStyle.Slam
                        ? _fsJuice.GetSlamAnimation((animationUid, sprite), spriteRotation, length, offset, anticipation, hold, angle.Theta < 0)
                        : GetSlashAnimation((animationUid, sprite), angle, spriteRotation, length, offset, anticipation, hold);
                _animation.Play(animationUid, slash, SlashAnimationKey);
                if (arcComponent.Fadeout)
                    _animation.Play(animationUid, GetFadeAnimation(sprite, length * 0.5f + anticipation + hold, length + 0.15f + anticipation + hold), FadeAnimationKey);
                break;
            case WeaponArcAnimation.Thrust:
                track = EnsureComp<TrackUserComponent>(animationUid);
                track.User = user;
                _animation.Play(animationUid, GetThrustAnimation((animationUid, sprite), offset, spriteRotation, length, anticipation, hold), ThrustAnimationKey);
                if (arcComponent.Fadeout)
                    _animation.Play(animationUid, GetFadeAnimation(sprite, length * 0.5f + anticipation + hold, length + 0.15f + anticipation + hold), FadeAnimationKey);
                break;
            case WeaponArcAnimation.None:
                juice = null;
                var (mapPos, mapRot) = TransformSystem.GetWorldPositionRotation(userXform);
                var worldPos = mapPos + (mapRot - userXform.LocalRotation).RotateVec(localPos);
                var newLocalPos = Vector2.Transform(worldPos, TransformSystem.GetInvWorldMatrix(xform.ParentUid));
                TransformSystem.SetLocalPositionNoLerp(animationUid, newLocalPos, xform);
                if (arcComponent.Fadeout)
                    _animation.Play(animationUid, GetFadeAnimation(sprite, 0f, 0.15f), FadeAnimationKey);
                break;
        }

        if (juice != null) // FINALSTAND
        {
            _fsJuice.TrackTrail(animationUid, user, juice, length + anticipation + hold);
            _fsJuice.PlaySwingEffect(user, juice);
            if (_fsJuice.GetStyle(weapon, juiceHeavy) != FSMeleeHeavyStyle.Spin)
                _fsJuice.PlaySlash(user, juice, localPos.ToWorldAngle(), (TransformSystem.GetWorldRotation(userXform) - userXform.LocalRotation).RotateVec(localPos), TryComp(weapon, out MeleeWeaponComponent? swingMelee) && swingMelee.SwingLeft, anticipation + _fsJuice.StrikeDelay(weapon, juiceHeavy, length));
        }
    }

    /// <summary>
    /// Makes the sprite move in a slash motion around the player.
    /// For example for wide swing animations.
    /// </summary>
    private Animation GetSlashAnimation(Entity<SpriteComponent> sprite, Angle arc, Angle spriteRotation, float length, float offset, float anticipation = 0f, float hold = 0f)
    {
        var startRotation = sprite.Comp.Rotation + (arc * 0.5f);
        var endRotation = sprite.Comp.Rotation - (arc * 0.5f);

        var startRotationOffset = startRotation.RotateVec(new Vector2(0f, -offset * 0.9f));
        var minRotationOffset = sprite.Comp.Rotation.RotateVec(new Vector2(0f, -offset * 1.1f));
        var endRotationOffset = endRotation.RotateVec(new Vector2(0f, -offset * 0.9f));

        startRotation += spriteRotation;
        endRotation += spriteRotation;

        // FINALSTAND: wind back before the swing and hold at the point of impact
        return new Animation()
        {
            Length = TimeSpan.FromSeconds(length + 0.05f + anticipation + hold),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation,endRotation,anticipation > 0f ? -0.15f : 0f), 0f),
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation,endRotation,0.0f), anticipation),
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation,endRotation,0.5f), length * 0.10f),
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation,endRotation,1.0f), length * 0.15f),
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation,endRotation,1.0f), hold),
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation,endRotation,0.9f), length * 0.20f),
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation,endRotation,0.80f), length * 0.6f, Easings.OutQuart)
                    },
                },
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startRotationOffset,endRotationOffset,0.0f) * (anticipation > 0f ? 0.8f : 1f), 0f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startRotationOffset,endRotationOffset,0.0f), anticipation),
                        new AnimationTrackProperty.KeyFrame(minRotationOffset, length * 0.10f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startRotationOffset,endRotationOffset,1.0f), length * 0.15f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startRotationOffset,endRotationOffset,1.0f), hold),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startRotationOffset,endRotationOffset,0.80f), length * 0.6f, Easings.OutQuart)
                    }
                },
            }
        };
    }

    /// <summary>
    /// Makes the sprite move in a thrust motion from the player towards the target, then slightly pulls back.
    /// For example for spears.
    /// </summary>
    private Animation GetThrustAnimation(Entity<SpriteComponent> sprite, float offset, Angle spriteRotation, float length, float anticipation = 0f, float hold = 0f)
    {
        var startOffset = sprite.Comp.Rotation.RotateVec(new Vector2(0f, 0f));
        var endOffset = sprite.Comp.Rotation.RotateVec(new Vector2(0f, -offset * 1.2f));

        _sprite.SetRotation(sprite.AsNullable(), sprite.Comp.Rotation + spriteRotation);

        // FINALSTAND: draw back before the stab and hold at full extension
        return new Animation()
        {
            Length = TimeSpan.FromSeconds(length + anticipation + hold),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, anticipation > 0f ? -0.25f : 0f), 0f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, 0f), anticipation),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, 0.65f), length * 0.10f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, 1f), length * 0.20f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, 1f), hold),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, 0.9f), length * 0.30f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, 0.7f), length * 0.60f, Easings.OutQuart)
                    }
                },
            }
        };
    }

    /// <summary>
    /// Makes the sprite slowly fade by gradually reducing its alpha value.
    /// Used at the end of attack animations so that the weapon sprite does not abruptly disappears.
    /// </summary>
    private Animation GetFadeAnimation(SpriteComponent sprite, float start, float end)
    {
        return new Animation
        {
            Length = TimeSpan.FromSeconds(end),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(sprite.Color, start),
                        new AnimationTrackProperty.KeyFrame(sprite.Color.WithAlpha(0f), end)
                    }
                }
            }
        };
    }

    /// <summary>
    /// Get the sprite offset animation to use for mob lunges.
    /// This is applied to the attacker to show who is attacking.
    /// </summary>
    private Animation GetLungeAnimation(Vector2 direction, float anticipation = 0f, float hold = 0f)
    {
        const float length = 0.1f;

        // FINALSTAND: lean back first and stay leaned in through the impact hold
        return new Animation
        {
            Length = TimeSpan.FromSeconds(length + anticipation + hold),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(direction.Normalized() * (anticipation > 0f ? -0.06f : 0f), anticipation),
                        new AnimationTrackProperty.KeyFrame(direction.Normalized() * 0.15f, length*0.4f),
                        new AnimationTrackProperty.KeyFrame(direction.Normalized() * 0.15f, hold),
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, length*0.6f),
                    },
                },
            },
        };
    }

    /// <summary>
    /// Updates the effect positions to follow the user
    /// </summary>
    private void UpdateEffects()
    {
        var query = EntityQueryEnumerator<TrackUserComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var arcComponent, out var xform))
        {
            if (arcComponent.User == null || EntityManager.Deleted(arcComponent.User))
                continue;

            var targetPos = TransformSystem.GetWorldPosition(arcComponent.User.Value);

            if (arcComponent.Offset != Vector2.Zero)
            {
                var entRotation = TransformSystem.GetWorldRotation(xform);
                targetPos += entRotation.RotateVec(arcComponent.Offset);
            }

            TransformSystem.SetWorldPosition(uid, targetPos);
        }
    }
}
