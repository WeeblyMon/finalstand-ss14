using System.Numerics;
using Content.Shared._FinalStand.Melee;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Animations;
using Robust.Shared.Configuration;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using DrawDepthTag = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._FinalStand.Melee;

// Leaves fading copies and a streak behind a dashing body, and eases the camera after your own dash instead of snapping it.
public sealed partial class FSMeleeDashVisualsSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
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

    // Seconds the camera takes to catch most of the way up, and how long after a dash it keeps easing.
    private const float CameraLag = 0.08f;
    private static readonly TimeSpan CameraSettle = TimeSpan.FromSeconds(0.5);
    private const float MaxCameraLag = 1.5f;

    private readonly Dictionary<EntityUid, float> _next = new();
    private readonly Dictionary<EntityUid, TimeSpan> _streaked = new();
    private Vector2 _eye;
    private MapId _eyeMap;
    private Vector2 _eyeLag;
    private TimeSpan _settleUntil;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMeleeDashComponent, GetEyeOffsetEvent>(OnEyeOffset);
    }

    private void OnEyeOffset(Entity<FSMeleeDashComponent> ent, ref GetEyeOffsetEvent args)
    {
        if (ent.Owner == _player.LocalEntity)
            args.Offset += _eyeLag;
    }

    // The camera trails the body during and just after a dash, so its speed and any correction at the end ease in.
    private void UpdateCamera(float frameTime, TimeSpan now)
    {
        if (_player.LocalEntity is not { } local || !TryComp<FSMeleeDashComponent>(local, out var dash))
        {
            _eyeLag = Vector2.Zero;
            return;
        }

        var (pos, map) = (_xform.GetWorldPosition(local), _xform.GetMapId(local));
        if (dash.Velocity != Vector2.Zero && now < dash.Until)
            _settleUntil = now + CameraSettle;

        if (now > _settleUntil || map != _eyeMap)
        {
            _eye = pos;
            _eyeMap = map;
            _eyeLag = Vector2.Zero;
            return;
        }

        _eye += (pos - _eye) * (1f - MathF.Exp(-frameTime / CameraLag));
        _eyeLag = _eye - pos;
        if (_eyeLag.Length() > MaxCameraLag)
        {
            _eyeLag = Vector2.Normalize(_eyeLag) * MaxCameraLag;
            _eye = pos + _eyeLag;
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        UpdateCamera(frameTime, now);
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
