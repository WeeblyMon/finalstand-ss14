using System.Numerics;
using Content.Client.Animations;
using Content.Client.Items;
using Content.Client.Items.Systems;
using Content.Shared._FinalStand.Melee;
using Content.Shared.CCVar;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Melee;

// A Shot heavy puts its weapon away and draws the gun at the target for the shot, flashing at the muzzle.
public sealed partial class FSMeleeShotVisualsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ItemSystem _item = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private AnimationPlayerSystem _animation = default!;

    private static readonly TimeSpan DrawTime = TimeSpan.FromSeconds(0.45);
    private static readonly EntProtoId DrawnGun = "FSMeleeDrawnGun";
    private static readonly EntProtoId MuzzleFlash = "MuzzleFlashEffect";
    private const float HoldReach = 0.45f;
    private const float MuzzleReach = 0.95f;
    private const float GunScale = 0.8f;
    private const float Kick = 0.14f;
    private const float FlashTime = 0.25f;

    private readonly Dictionary<EntityUid, TimeSpan> _seen = new();
    private readonly HashSet<EntityUid> _drawn = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMeleeShotStateComponent, GetInhandVisualsEvent>(OnInhandVisuals, after: [typeof(ItemSystem)]);
        SubscribeLocalEvent<FSMeleeShotStateComponent, ComponentShutdown>(OnShutdown);
        Subs.ItemStatus<FSMeleeShotStateComponent>(ent => new FSMeleeShotStatusControl(ent));
    }

    private void OnShutdown(Entity<FSMeleeShotStateComponent> ent, ref ComponentShutdown args)
    {
        _seen.Remove(ent);
        _drawn.Remove(ent);
    }

    // The weapon is put away while the gun is out.
    private void OnInhandVisuals(Entity<FSMeleeShotStateComponent> ent, ref GetInhandVisualsEvent args)
    {
        if (_drawn.Contains(ent))
            args.Layers.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSMeleeShotStateComponent, FSMeleeJuiceComponent>();
        while (query.MoveNext(out var uid, out var shots, out var juice))
        {
            var drawn = juice.ShotSprite != null && now - shots.LastShot < DrawTime;
            if (shots.LastShot != TimeSpan.Zero && (!_seen.TryGetValue(uid, out var seen) || seen != shots.LastShot))
            {
                _seen[uid] = shots.LastShot;
                if (drawn)
                    Fire(uid, shots, juice);
            }

            if (drawn == _drawn.Contains(uid))
                continue;

            if (drawn)
                _drawn.Add(uid);
            else
                _drawn.Remove(uid);

            _item.VisualsChanged(uid);
        }
    }

    private void Fire(EntityUid weapon, FSMeleeShotStateComponent shots, FSMeleeJuiceComponent juice)
    {
        if (juice.ShotSprite is not { } gunSprite
            || !_container.TryGetContainingContainer(weapon, out var hand)
            || !HasComp<HandsComponent>(hand.Owner))
            return;

        var holder = hand.Owner;
        var aim = shots.LastAim;
        var at = _xform.GetMapCoordinates(holder);

        var gun = Spawn(DrawnGun, at);
        var sprite = Comp<SpriteComponent>(gun);
        _sprite.AddLayer((gun, sprite), gunSprite);
        // Keeps the gun upright when it points left.
        _sprite.SetScale((gun, sprite), new Vector2(GunScale, aim.ToVec().X < 0f ? -GunScale : GunScale));
        _xform.SetWorldRotationNoLerp(gun, aim);
        var track = EnsureComp<TrackUserComponent>(gun);
        track.User = holder;
        track.Offset = new Vector2(HoldReach, 0f);
        EnsureComp<TimedDespawnComponent>(gun).Lifetime = (float) DrawTime.TotalSeconds;
        _animation.Play(gun, Recoil(), "fs-melee-shot-recoil");

        if (!_cfg.GetCVar(CCVars.FSMeleeEffects))
            return;

        var flash = Spawn(MuzzleFlash, at.Offset(aim.ToVec() * MuzzleReach));
        _xform.SetWorldRotationNoLerp(flash, aim);
        _animation.Play(flash, Fade(FlashTime), "fs-melee-shot-flash");
    }

    // Kicks back on the shot, then settles before the gun is put away.
    private static Animation Recoil()
    {
        var length = (float) DrawTime.TotalSeconds;
        return new Animation
        {
            Length = DrawTime,
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(-Kick, 0f), 0.05f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0.2f),
                    },
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Color.White, 0f),
                        new AnimationTrackProperty.KeyFrame(Color.White, length - 0.1f),
                        new AnimationTrackProperty.KeyFrame(Color.White.WithAlpha(0f), 0.1f),
                    },
                },
            },
        };
    }

    private static Animation Fade(float length)
    {
        return new Animation
        {
            Length = TimeSpan.FromSeconds(length),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Color.White, 0f),
                        new AnimationTrackProperty.KeyFrame(Color.White.WithAlpha(0f), length),
                    },
                },
            },
        };
    }
}
