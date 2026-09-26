// Drives FSPickupVisuals: a slow spin plus a sine bob, phase-offset per entity.
using System.Numerics;
using Content.Shared._FinalStand.Perks;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Perks;

public sealed class FSPickupVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var time = (float) _timing.RealTime.TotalSeconds;
        var query = EntityQueryEnumerator<FSPickupVisualsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var visuals, out var sprite))
        {
            var t = time + (uid.Id % 97) * 0.37f;
            _sprite.SetRotation((uid, sprite), Angle.FromDegrees(t * visuals.SpinDegreesPerSecond % 360f));

            var bob = MathF.Sin(t / visuals.BobSeconds * MathF.Tau) * visuals.BobHeight;
            _sprite.SetOffset((uid, sprite), new Vector2(0f, bob));
        }
    }
}
