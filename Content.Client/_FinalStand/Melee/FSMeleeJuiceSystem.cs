using System.Numerics;
using Content.Client._FinalStand.Particles;
using Content.Client.Animations;
using Content.Client.Eye;
using Content.Client.Weapons.Melee;
using Content.Client.Weapons.Melee.Components;
using Content.Shared._FinalStand.Melee;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Animations;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Melee;

public sealed partial class FSMeleeJuiceSystem : SharedFSMeleeJuiceSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private FSParticleSystem _particles = default!;
    [Dependency] private MeleeWeaponSystem _melee = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private const string FlinchKey = "fs-melee-flinch";
    private const float MaxShake = 0.35f;
    private const int BurstBudget = 8;
    private const int MaxJitterTargets = 6;
    private const int FatigueHits = 10;
    private const float FatigueReset = 1.5f;
    private const float ZoomDuration = 0.1f;
    private const float HoldWindow = 0.25f;

    public sealed class Trail
    {
        public EntityUid Arc;
        public MapId Map;
        public readonly List<(Vector2 Position, float Time)> Points = new();
        public float Duration;
        public float Width;
        public float Opacity;
        public float Until;
        public Color Color;
    }

    public readonly List<Trail> Trails = new();

    private readonly record struct PendingImpact(float Due, EntityUid User, FSMeleeJuiceProfile Profile, List<EntityUid> Targets, Vector2 Direction, float Hold, bool Lethal);

    private readonly List<PendingImpact> _impacts = new();

    private readonly record struct PendingSlash(float Due, EntityUid User, FSMeleeJuiceProfile Profile, Angle Rotation, bool Mirror);

    private readonly List<PendingSlash> _slashes = new();
    public float RealTime => (float) Timing.RealTime.TotalSeconds;

    private readonly Dictionary<EntityUid, (float Hold, float Time, EntityUid Weapon, FSMeleeJuiceProfile Profile)> _pending = new();
    private int _burstsThisFrame;
    private int _streak;
    private float _lastHit;
    private EntityUid? _streakWeapon;
    private float _zoomPunch;
    private float _zoomStart = float.MinValue;
    private FSMeleeTrailOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<FSMeleeHitJuiceEvent>(OnHitEvent);
        SubscribeNetworkEvent<FSMeleeKillJuiceEvent>(OnKillEvent);
        SubscribeLocalEvent<FSMeleeShakeComponent, GetEyeOffsetEvent>(OnEyeOffset);

        UpdatesAfter.Add(typeof(EyeLerpingSystem));

        _overlay = new FSMeleeTrailOverlay(this);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }

    protected override void OnHit(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, List<EntityUid> targets, Vector2 direction, bool heavy, bool finisher, bool lethal, MeleeHitEvent args)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        Play(user, weapon, targets, direction, heavy, finisher, 0, lethal);
    }

    private void OnHitEvent(FSMeleeHitJuiceEvent ev)
    {
        if (!TryGetEntity(ev.User, out var user)
            || !TryGetEntity(ev.Weapon, out var weaponUid)
            || !TryComp<FSMeleeJuiceComponent>(weaponUid, out var juice))
            return;

        var targets = new List<EntityUid>();
        foreach (var net in ev.Targets)
        {
            if (TryGetEntity(net, out var target) && !TerminatingOrDeleted(target))
                targets.Add(target.Value);
        }

        Play(user.Value, (weaponUid.Value, juice), targets, ev.Direction, ev.Heavy, ev.Finisher, ev.Strike, ev.Lethal);

        if (ev.Strike > 0 && HasComp<SpriteComponent>(user) && TryComp<MeleeWeaponComponent>(weaponUid, out var melee))
            ReplaySwing(user.Value, weaponUid.Value, melee, ev.Direction, ev.Heavy);
    }

    private void OnKillEvent(FSMeleeKillJuiceEvent ev)
    {
        if (!TryGetEntity(ev.Target, out var target)
            || !TryGetEntity(ev.Weapon, out var weapon)
            || !TryComp<FSMeleeJuiceComponent>(weapon, out var juice))
            return;

        var profile = juice.Get(ev.Heavy, ev.Finisher);
        if (_cfg.GetCVar(CCVars.FSMeleeEffects))
        {
            SpawnEffect(profile.KillEffect, target.Value, Angle.Zero);
            if (profile.KillEmitter is { } emitter)
                _particles.Burst(emitter, target.Value, profile.ParticleTint, 1.5f);
        }

        if (TryGetEntity(ev.User, out var user) && user == _player.LocalEntity && RealTime - _zoomStart > 0.3f)
            StartZoom(profile.KillZoom);
    }

    private void Play(EntityUid user, Entity<FSMeleeJuiceComponent> weapon, List<EntityUid> targets, Vector2 direction, bool heavy, bool finisher, int strike, bool lethal)
    {
        var profile = weapon.Comp.Get(heavy, finisher);
        var hold = profile.Hitstop + (lethal ? profile.KillHitstopBonus : 0f);
        if (strike > 0)
            hold *= 0.6f;

        _pending[user] = (hold, RealTime, weapon.Owner, profile);

        if (user == _player.LocalEntity)
        {
            var now = RealTime;
            _streak = weapon.Owner == _streakWeapon && now - _lastHit < FatigueReset ? _streak + 1 : 1;
            _streakWeapon = weapon.Owner;
            _lastHit = now;
        }

        // The hit lands on click, but the swing only reaches the target at its impact keyframe.
        _impacts.Add(new PendingImpact(RealTime + ImpactDelay(weapon, profile, heavy), user, profile, targets, direction.Normalized(), hold, lethal));
    }

    private void Impact(PendingImpact impact)
    {
        var (_, user, profile, targets, dir, hold, lethal) = impact;
        for (var i = 0; i < targets.Count && i < MaxJitterTargets; i++)
        {
            if (!TerminatingOrDeleted(targets[i]))
                Jitter(targets[i], dir, hold + 0.06f, 0.05f + profile.Trauma * 0.1f);
        }

        foreach (var target in targets)
        {
            if (_burstsThisFrame >= BurstBudget || !_cfg.GetCVar(CCVars.FSMeleeEffects))
                break;
            if (TerminatingOrDeleted(target))
                continue;

            _burstsThisFrame++;
            SpawnEffect(profile.HitEffect, target, dir.ToWorldAngle() + Angle.FromDegrees(profile.HitEffectAngle));
            if (profile.HitEmitter is { } emitter)
                _particles.Burst(emitter, target, profile.ParticleTint);
        }

        if (user != _player.LocalEntity)
            return;

        AddTrauma(user, profile.Trauma * Math.Min(1f + 0.25f * (targets.Count - 1), 2f), dir, profile.ShakeMode);

        if (lethal)
            StartZoom(profile.KillZoom);
    }

    private float ImpactDelay(Entity<FSMeleeJuiceComponent> weapon, FSMeleeJuiceProfile profile, bool heavy)
    {
        if (!TryComp<MeleeWeaponComponent>(weapon, out var melee) || melee.AttackRate <= 0f)
            return 0f;

        var length = 0.6f / melee.AttackRate;
        if (heavy && weapon.Comp.HeavyIsSpin)
            return profile.Anticipation + length * 0.35f;

        var arc = heavy ? melee.WideAnimation : melee.Animation;
        if (!_prototypes.Index(arc).TryGetComponent<WeaponArcVisualsComponent>(out var visuals, EntityManager.ComponentFactory))
            return 0f;

        return visuals.Animation switch
        {
            WeaponArcAnimation.Slash => profile.Anticipation + length * 0.25f,
            WeaponArcAnimation.Thrust => profile.Anticipation + length * 0.3f,
            _ => 0f,
        };
    }

    private void SpawnEffect(EntProtoId? proto, EntityUid at, Angle rotation)
    {
        if (proto == null || TerminatingOrDeleted(at))
            return;

        var effect = Spawn(proto.Value, Xform.GetMapCoordinates(at));
        if (TryComp<SpriteComponent>(effect, out var sprite))
            _sprite.SetRotation((effect, sprite), rotation);
    }

    public void PlaySwingEffect(EntityUid user, FSMeleeJuiceProfile profile)
    {
        if (profile.SwingEffect is not { } proto || !_cfg.GetCVar(CCVars.FSMeleeEffects))
            return;

        var effect = Spawn(proto, Xform.GetMapCoordinates(user));
        EnsureComp<TrackUserComponent>(effect).User = user;
    }

    public void PlaySlash(EntityUid user, FSMeleeJuiceProfile profile, Angle rotation, bool mirror, float delay)
    {
        if (profile.SlashEffect != null && _cfg.GetCVar(CCVars.FSMeleeEffects))
            _slashes.Add(new PendingSlash(RealTime + delay, user, profile, rotation, mirror));
    }

    private void SpawnSlash(PendingSlash slash)
    {
        if (TerminatingOrDeleted(slash.User))
            return;

        var effect = Spawn(slash.Profile.SlashEffect, Xform.GetMapCoordinates(slash.User));
        EnsureComp<TrackUserComponent>(effect).User = slash.User;
        if (!TryComp<SpriteComponent>(effect, out var sprite))
            return;

        var scale = slash.Profile.SlashScale;
        _sprite.SetRotation((effect, sprite), slash.Rotation);
        _sprite.SetScale((effect, sprite), new Vector2(slash.Mirror ? -scale : scale, scale));
        _sprite.SetColor((effect, sprite), slash.Profile.SlashColor);
    }

    private void ReplaySwing(EntityUid user, EntityUid weapon, MeleeWeaponComponent melee, Vector2 direction, bool heavy)
    {
        var xform = Transform(user);
        var parentRotation = Xform.GetWorldRotation(xform) - xform.LocalRotation;
        var localPos = (-parentRotation).RotateVec(direction);
        var length = Math.Max(0.1f, melee.Range - 0.2f);
        if (localPos.Length() > length)
            localPos = localPos.Normalized() * length;

        _melee.DoLunge(user, weapon, melee.Angle, localPos, heavy ? melee.WideAnimation : melee.Animation, false);
    }

    private void Jitter(EntityUid target, Vector2 dir, float duration, float amplitude)
    {
        if (!TryComp<SpriteComponent>(target, out var sprite) || _animation.HasRunningAnimation(target, FlinchKey))
            return;

        var origin = sprite.Offset;
        var push = dir * amplitude;
        var side = new Vector2(-dir.Y, dir.X) * amplitude * 0.5f;

        _animation.Play(target, new Animation
        {
            Length = TimeSpan.FromSeconds(duration),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(origin + push, 0f),
                        new AnimationTrackProperty.KeyFrame(origin + push * 0.4f + side, duration * 0.25f),
                        new AnimationTrackProperty.KeyFrame(origin + push * 0.8f - side, duration * 0.25f),
                        new AnimationTrackProperty.KeyFrame(origin + push * 0.3f, duration * 0.25f),
                        new AnimationTrackProperty.KeyFrame(origin, duration * 0.25f),
                    },
                },
            },
        }, FlinchKey);
    }

    private void AddTrauma(EntityUid user, float amount, Vector2 dir, FSMeleeShakeMode mode)
    {
        if (amount <= 0f)
            return;

        var shake = EnsureComp<FSMeleeShakeComponent>(user);
        shake.Trauma = mode == FSMeleeShakeMode.Sustained
            ? Math.Max(shake.Trauma, amount)
            : Math.Min(1f, shake.Trauma + amount);
        shake.Direction = dir;
        shake.Mode = mode;
    }

    private void StartZoom(float amount)
    {
        if (amount <= 0f)
            return;

        _zoomPunch = amount;
        _zoomStart = RealTime;
    }

    private void OnEyeOffset(Entity<FSMeleeShakeComponent> ent, ref GetEyeOffsetEvent args)
    {
        var shake = ent.Comp;
        if (shake.Trauma <= 0f)
            return;

        var intensity = _cfg.GetCVar(CCVars.ScreenShakeIntensity);
        var strength = MaxShake * shake.Trauma * shake.Trauma * intensity;
        var t = shake.Time;
        var along = Noise(t, 0f);
        var across = Noise(t, 7.3f);

        if (shake.Mode == FSMeleeShakeMode.Directional)
        {
            var perp = new Vector2(-shake.Direction.Y, shake.Direction.X);
            args.Offset += (shake.Direction * along + perp * across * 0.35f) * strength;
        }
        else
        {
            args.Offset += new Vector2(along, across) * strength;
        }
    }

    private static float Noise(float t, float seed)
    {
        return MathF.Sin(t * 41f + seed) * 0.5f
               + MathF.Sin(t * 23.7f + seed * 2.1f) * 0.3f
               + MathF.Sin(t * 67.1f + seed * 0.7f) * 0.2f;
    }

    public float TakeHold(EntityUid user)
    {
        if (!_pending.Remove(user, out var pending) || RealTime - pending.Time > HoldWindow)
            return 0f;

        return pending.Hold;
    }

    public FSMeleeJuiceProfile? GetProfile(EntityUid user, EntityUid weapon, bool heavy)
    {
        if (_pending.TryGetValue(user, out var pending) && pending.Weapon == weapon && RealTime - pending.Time <= HoldWindow)
            return pending.Profile;

        return TryComp<FSMeleeJuiceComponent>(weapon, out var juice) ? juice.Get(heavy) : null;
    }

    public bool IsSpin(EntityUid weapon, bool heavy)
    {
        return heavy && TryComp<FSMeleeJuiceComponent>(weapon, out var juice) && juice.HeavyIsSpin;
    }

    public void TrackTrail(EntityUid arc, EntityUid user, FSMeleeJuiceProfile profile, float swingLength)
    {
        if (profile.TrailDuration <= 0f || !_cfg.GetCVar(CCVars.FSMeleeEffects))
            return;

        var opacity = user == _player.LocalEntity && _streak >= FatigueHits ? 0.5f : 1f;
        Trails.Add(new Trail
        {
            Arc = arc,
            Map = Transform(arc).MapID,
            Duration = profile.TrailDuration,
            Width = profile.TrailWidth,
            Color = profile.TrailColor,
            Opacity = opacity,
            Until = RealTime + swingLength,
        });
    }

    public Animation GetSpinAnimation(Entity<SpriteComponent> sprite, Angle spriteRotation, float length, float offset, float anticipation, float hold, bool reverse)
    {
        const int steps = 8;
        var sign = reverse ? -1 : 1;
        var start = sprite.Comp.Rotation + Math.PI;
        var spin = length * 0.35f;

        var rotation = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Rotation),
        };
        var position = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Offset),
        };

        void Key(Angle angle, float delta, Func<float, float>? easing = null)
        {
            rotation.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(angle + spriteRotation, delta, easing));
            position.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(angle.RotateVec(new Vector2(0f, -offset)), delta, easing));
        }

        Key(start + sign * 0.4, 0f);
        Key(start, anticipation);
        for (var i = 1; i <= steps; i++)
        {
            Key(start - sign * (Math.Tau * i / steps), spin / steps);
        }

        Key(start - sign * Math.Tau, hold);
        Key(start - sign * (Math.Tau + 0.25), length * 0.3f, Easings.OutQuart);

        return new Animation
        {
            Length = TimeSpan.FromSeconds(anticipation + spin + hold + length * 0.3f),
            AnimationTracks = { rotation, position },
        };
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _burstsThisFrame = 0;

        var shakes = EntityQueryEnumerator<FSMeleeShakeComponent>();
        while (shakes.MoveNext(out var shake))
        {
            if (shake.Trauma <= 0f)
                continue;

            shake.Time += frameTime;
            shake.Trauma = Math.Max(0f, shake.Trauma - frameTime * (shake.Mode == FSMeleeShakeMode.Sustained ? 2.5f : 1.8f));
        }

        var now = RealTime;
        for (var i = _slashes.Count - 1; i >= 0; i--)
        {
            if (_slashes[i].Due > now)
                continue;

            var slash = _slashes[i];
            _slashes.RemoveAt(i);
            SpawnSlash(slash);
        }

        for (var i = _impacts.Count - 1; i >= 0; i--)
        {
            if (_impacts[i].Due > now)
                continue;

            var impact = _impacts[i];
            _impacts.RemoveAt(i);
            Impact(impact);
        }

        UpdateTrails();
        UpdateZoom();
    }

    private void UpdateTrails()
    {
        var now = RealTime;
        for (var i = Trails.Count - 1; i >= 0; i--)
        {
            var trail = Trails[i];
            SpriteComponent? sprite = null;
            var live = now < trail.Until && !TerminatingOrDeleted(trail.Arc) && TryComp(trail.Arc, out sprite);

            if (live)
            {
                var (pos, rot) = Xform.GetWorldPositionRotation(trail.Arc);
                trail.Points.Add((pos + rot.RotateVec(sprite!.Offset * 1.25f), now));
            }

            trail.Points.RemoveAll(p => now - p.Time > trail.Duration);
            if (!live && trail.Points.Count == 0)
                Trails.RemoveAt(i);
        }
    }

    private void UpdateZoom()
    {
        var elapsed = RealTime - _zoomStart;
        if (elapsed >= ZoomDuration
            || _player.LocalEntity is not { } local
            || !HasComp<LerpingEyeComponent>(local)
            || !TryComp<EyeComponent>(local, out var eye))
            return;

        var remaining = 1f - elapsed / ZoomDuration;
        _eye.SetZoom(local, eye.Zoom * (1f - _zoomPunch * remaining * remaining), eye);
    }
}
