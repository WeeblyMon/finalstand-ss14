using Content.Shared._FinalStand.Upgrades.Effects;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Upgrades.Effects;

public sealed partial class FSOverclockedGlowSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;

    // white → hot red as spool ramps 0→1
    private static readonly Color MaxGlow = new(1f, 0.15f, 0.05f);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Spool only changes per tick; skip the re-predicted ones.
        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<FSOverclockedComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var oc, out var sprite))
        {
            var color = Color.InterpolateBetween(Color.White, MaxGlow, oc.Spool);

            if (sprite.Color == color)
                continue;

            _sprite.SetColor((uid, sprite), color);
        }
    }
}
