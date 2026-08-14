using Content.Shared._FinalStand.Upgrades.Effects;
using Robust.Client.GameObjects;

namespace Content.Client._FinalStand.Upgrades.Effects;

public sealed partial class FSOverclockedGlowSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    // white → hot red as spool ramps 0→1
    private static readonly Color MaxGlow = new(1f, 0.15f, 0.05f);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

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
