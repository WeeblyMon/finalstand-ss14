// Shows the half-built mech on the Mech Bay pad, further along with each part fitted.
using Content.Shared._FinalStand.Mech;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Mech;

public sealed partial class FSMechBayVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMechBayComponent, AfterAutoHandleStateEvent>((uid, comp, _) => Refresh((uid, comp)));
        SubscribeLocalEvent<FSMechBayComponent, ComponentStartup>((uid, comp, _) => Refresh((uid, comp)));
        _overlays.AddOverlay(new FSMechBayOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSMechBayOverlay>();
    }

    private void Refresh(Entity<FSMechBayComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite)
            || !_sprite.LayerMapTryGet((ent.Owner, sprite), FSMechBayVisualLayers.Stage, out var layer, false))
            return;

        var done = ent.Comp.Installed.Count;
        var total = Math.Max(1, ent.Comp.Slots.Count);
        _sprite.LayerSetVisible((ent.Owner, sprite), layer, done > 0);
        if (done > 0)
            _sprite.LayerSetRsiState((ent.Owner, sprite), layer, $"{ent.Comp.FramePrefix}{done * ent.Comp.FrameStates / total}");
    }
}
