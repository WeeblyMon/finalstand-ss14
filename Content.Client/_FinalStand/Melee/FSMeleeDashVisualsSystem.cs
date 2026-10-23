using System.Numerics;
using Content.Shared._FinalStand.Melee;
using Content.Shared.CCVar;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Configuration;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using DrawDepthTag = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._FinalStand.Melee;

// Leaves fading copies of a dashing body behind it, so the dash reads as movement.
public sealed class FSMeleeDashVisualsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private FSMeleeJuiceSystem _juice = default!;

    private const float Interval = 0.03f;
    private const float FadeTime = 0.25f;
    private static readonly Color Tint = Color.FromHex("#6fd0ffb0");
    private static readonly Color Glow = Color.FromHex("#2f8cff70");
    private static readonly Color Core = Color.FromHex("#e0f6ffe0");

    private readonly Dictionary<EntityUid, float> _next = new();
    private readonly Dictionary<EntityUid, TimeSpan> _streaked = new();

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var real = (float) _timing.RealTime.TotalSeconds;
        var effects = _cfg.GetCVar(CCVars.FSMeleeEffects);
        var query = EntityQueryEnumerator<FSMeleeDashComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var dash, out var sprite))
        {
            if (!effects || dash.Velocity == Vector2.Zero || now >= dash.Until)
            {
                _next.Remove(uid);
                _streaked.Remove(uid);
                continue;
            }

            if (!_streaked.TryGetValue(uid, out var streaked) || streaked != dash.Until)
            {
                _streaked[uid] = dash.Until;
                var left = (float) (dash.Until - now).TotalSeconds;
                _juice.TrackDash(uid, left, 0.35f, 0.32f, Glow);
                _juice.TrackDash(uid, left, 0.25f, 0.09f, Core);
            }

            if (_next.TryGetValue(uid, out var next) && real < next)
                continue;

            _next[uid] = real + Interval;
            Afterimage((uid, sprite));
        }
    }

    private void Afterimage(Entity<SpriteComponent> body)
    {
        var clone = Spawn("clientsideclone", _xform.GetMapCoordinates(body));
        var sprite = Comp<SpriteComponent>(clone);
        _sprite.CopySprite((body, body.Comp), (clone, sprite));
        _sprite.SetVisible((clone, sprite), true);
        _sprite.SetColor((clone, sprite), Tint);
        _sprite.SetDrawDepth((clone, sprite), (int) DrawDepthTag.SmallMobs);
        _xform.SetWorldRotationNoLerp(clone, _xform.GetWorldRotation(body));
        EnsureComp<TimedDespawnComponent>(clone).Lifetime = FadeTime;

        _animation.Play(clone, new Animation
        {
            Length = TimeSpan.FromSeconds(FadeTime),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Tint, 0f),
                        new AnimationTrackProperty.KeyFrame(Tint.WithAlpha(0f), FadeTime),
                    },
                },
            },
        }, "fs-melee-afterimage");
    }
}
