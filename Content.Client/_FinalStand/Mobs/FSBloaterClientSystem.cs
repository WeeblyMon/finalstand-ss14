using Content.Shared._FinalStand.Mobs;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Mobs;

public sealed partial class FSBloaterClientSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _flashStart = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSBloaterFlashingComponent, ComponentStartup>(OnFlashStart);
        SubscribeLocalEvent<FSBloaterFlashingComponent, ComponentShutdown>(OnFlashEnd);
    }

    private void OnFlashStart(EntityUid uid, FSBloaterFlashingComponent _, ComponentStartup args)
        => _flashStart[uid] = _timing.CurTime;

    private void OnFlashEnd(EntityUid uid, FSBloaterFlashingComponent _, ComponentShutdown args)
    {
        _flashStart.Remove(uid);
        if (TryComp<SpriteComponent>(uid, out var sprite))
            sprite.Color = Color.White;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var (uid, startTime) in _flashStart)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite)) continue;
            var elapsed = (_timing.CurTime - startTime).TotalSeconds;
            // 2 flashes × 0.3s each: on for 0.15s, off for 0.15s
            var phase = elapsed % 0.3;
            sprite.Color = phase < 0.15 ? Color.Red : Color.White;
        }
    }
}
