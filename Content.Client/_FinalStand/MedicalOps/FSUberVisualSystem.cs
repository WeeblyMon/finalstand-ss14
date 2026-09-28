using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

// Tints Übered players toward the medic's beam colour, with TF2's flicker in the last two seconds.
public sealed partial class FSUberVisualSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly TimeSpan FlickerWindow = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSUberedComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<FSUberedComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetColor((ent, sprite), Color.White);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var time = (float) _timing.RealTime.TotalSeconds;

        var query = EntityQueryEnumerator<FSUberedComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var ubered, out var sprite))
        {
            var left = ubered.EndTime - now;
            var shine = 0.55f + 0.25f * MathF.Sin(time * 5f);

            if (left < FlickerWindow)
            {
                var rate = 4f + 8f * (1f - (float) (left / FlickerWindow));
                if (MathF.Sin(time * rate * MathF.Tau) < 0f)
                    shine = 0f;
            }

            _sprite.SetColor((uid, sprite), Color.InterpolateBetween(Color.White, ubered.SourceColor, shine));
        }
    }
}
