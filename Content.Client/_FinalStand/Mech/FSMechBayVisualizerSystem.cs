// Shows the half-built Durand on the Mech Bay pad, one sprite per finished stage.
using Content.Shared._FinalStand.Mech;
using Robust.Client.GameObjects;

namespace Content.Client._FinalStand.Mech;

public sealed partial class FSMechBayVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMechBayComponent, AfterAutoHandleStateEvent>((uid, comp, _) => Refresh((uid, comp)));
        SubscribeLocalEvent<FSMechBayComponent, ComponentStartup>((uid, comp, _) => Refresh((uid, comp)));
    }

    private void Refresh(Entity<FSMechBayComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite)
            || !_sprite.LayerMapTryGet((ent.Owner, sprite), FSMechBayVisualLayers.Stage, out var layer, false))
            return;

        var done = ent.Comp.Stage;
        _sprite.LayerSetVisible((ent.Owner, sprite), layer, done > 0);
        if (done > 0 && done <= ent.Comp.Stages.Count)
            _sprite.LayerSetRsiState((ent.Owner, sprite), layer, ent.Comp.Stages[done - 1].State);
    }
}
