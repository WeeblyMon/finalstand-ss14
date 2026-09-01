using Content.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;

namespace Content.Shared._FinalStand.Weapons;

public sealed class FSPiercesGlassSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSPiercesGlassComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<FSPiercesGlassComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<FixturesComponent>(args.OtherEntity, out var fixtures))
            return;

        var layer = 0;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (fixture.Hard)
                layer |= fixture.CollisionLayer;
        }

        if ((layer & (int) CollisionGroup.BulletImpassable) == 0)
            return;

        if ((layer & (int) CollisionGroup.Opaque) != 0)
            return;

        args.Cancelled = true;
    }
}
