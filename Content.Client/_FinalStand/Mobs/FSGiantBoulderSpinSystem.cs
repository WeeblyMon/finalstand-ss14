using Content.Shared._FinalStand.Mobs;
using Robust.Client.GameObjects;

namespace Content.Client._FinalStand.Mobs;

public sealed class FSGiantBoulderSpinSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<FSGiantBoulderComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var boulder, out var sprite))
            _sprite.SetRotation((uid, sprite), sprite.Rotation + boulder.SpinRate * frameTime);
    }
}
