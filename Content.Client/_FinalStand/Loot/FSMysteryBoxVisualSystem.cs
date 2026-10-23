using System.Numerics;
using Content.Shared._FinalStand.Loot;
using Content.Shared.Random.Helpers;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Loot;

public sealed partial class FSMysteryBoxVisualSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly EntProtoId TopperProto = "FSMysteryBoxTopper";

    private const string LidLayer = "lid";
    private const string DisplayLayer = "display";

    private static readonly Vector2 LidLift = new(0f, 0.55f);
    private const float LidTilt = -14f;
    private const float PopTime = 0.3f;
    private const float RiseTime = 0.6f;
    private const float FloatHeight = 0.75f;
    private const float SinkTime = 2.5f;
    private const float FastSwap = 0.07f;
    private const float SlowSwap = 0.4f;

    private FSMysteryBoxLabelOverlay _label = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMysteryBoxComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<FSMysteryBoxComponent, ComponentShutdown>(OnShutdown);
        _label = new FSMysteryBoxLabelOverlay();
        _overlays.AddOverlay(_label);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_label);
    }

    private void OnState(Entity<FSMysteryBoxComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ent.Comp.NextIcon = TimeSpan.Zero;
    }

    private void OnShutdown(Entity<FSMysteryBoxComponent> ent, ref ComponentShutdown args)
    {
        if (Exists(ent.Comp.Topper))
            QueueDel(ent.Comp.Topper);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSMysteryBoxComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var box, out var boxSprite, out var xform))
        {
            if (xform.MapID == MapId.Nullspace)
                continue;

            if (!TryComp<SpriteComponent>(box.Topper, out var sprite))
            {
                box.Topper = SpawnAttachedTo(TopperProto, new EntityCoordinates(uid, Vector2.Zero));
                box.NextIcon = TimeSpan.Zero;
                _sprite.LayerSetVisible((uid, boxSprite), LidLayer, false);
                continue;
            }

            Entity<SpriteComponent> ent = (box.Topper.Value, sprite);
            var elapsed = (float) (now - box.StateStart).TotalSeconds;
            var left = (float) (box.StateEnd - now).TotalSeconds;
            var total = (float) (box.StateEnd - box.StateStart).TotalSeconds;

            switch (box.State)
            {
                case FSMysteryBoxState.Spinning:
                    SetLid(ent, Pop(elapsed / PopTime));
                    if (now >= box.NextIcon)
                    {
                        ShowRandomIcon(ent, box);
                        var swap = MathHelper.Lerp(FastSwap, SlowSwap, MathF.Pow(Math.Clamp(elapsed / total, 0f, 1f), 2f));
                        box.NextIcon = now + TimeSpan.FromSeconds(swap);
                    }
                    SetDisplay(ent, FloatHeight * EaseOut(elapsed / RiseTime), 1f);
                    break;
                case FSMysteryBoxState.Offering:
                    SetLid(ent, 1f);
                    if (now >= box.NextIcon && box.Prize is { } prize)
                    {
                        _sprite.LayerSetTexture(ent.AsNullable(), DisplayLayer, _sprite.GetPrototypeIcon(prize).Default);
                        box.NextIcon = TimeSpan.MaxValue;
                    }
                    var sink = Math.Clamp(left / SinkTime, 0f, 1f);
                    var bob = MathF.Sin(elapsed * 3f) * 0.04f;
                    SetDisplay(ent, MathHelper.Lerp(0.1f, FloatHeight, sink) + bob, sink);
                    break;
                case FSMysteryBoxState.Closing:
                    SetLid(ent, total > 0f ? Math.Clamp(left / total, 0f, 1f) : 0f);
                    _sprite.LayerSetVisible(ent.AsNullable(), DisplayLayer, false);
                    break;
                default:
                    SetLid(ent, 0f);
                    _sprite.LayerSetVisible(ent.AsNullable(), DisplayLayer, false);
                    box.NextIcon = TimeSpan.Zero;
                    break;
            }
        }
    }

    private void ShowRandomIcon(Entity<SpriteComponent> ent, FSMysteryBoxComponent box)
    {
        if (box.Tiers.Count == 0 || !_protoMan.TryIndex(_random.Pick(box.Tiers).Pool, out var pool) || pool.Weights.Count == 0)
            return;

        _sprite.LayerSetTexture(ent.AsNullable(), DisplayLayer, _sprite.GetPrototypeIcon(_random.Pick(pool.Weights)).Default);
    }

    private void SetLid(Entity<SpriteComponent> ent, float open)
    {
        _sprite.LayerSetOffset(ent.AsNullable(), LidLayer, LidLift * open);
        _sprite.LayerSetRotation(ent.AsNullable(), LidLayer, Angle.FromDegrees(LidTilt * open));
    }

    private void SetDisplay(Entity<SpriteComponent> ent, float height, float alpha)
    {
        _sprite.LayerSetVisible(ent.AsNullable(), DisplayLayer, true);
        _sprite.LayerSetOffset(ent.AsNullable(), DisplayLayer, new Vector2(0f, height));
        _sprite.LayerSetColor(ent.AsNullable(), DisplayLayer, Color.White.WithAlpha(alpha));
    }

    private static float Pop(float t)
    {
        t = Math.Clamp(t, 0f, 1f) - 1f;
        return 1f + 2.70158f * t * t * t + 1.70158f * t * t;
    }

    private static float EaseOut(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return 1f - (1f - t) * (1f - t);
    }
}
