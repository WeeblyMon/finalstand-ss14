using Content.Shared._FinalStand.Deployables;
using Robust.Client.GameObjects;
using Robust.Shared.Analyzers;

namespace Content.Client._FinalStand.Deployables;

// Mirrors FSGrenadePackVisualsSystem - greys out the held item's sprite once Stock hits 0.
public sealed class FSDeployableItemVisualsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSDeployableItemComponent, AfterAutoHandleStateEvent>(OnStateHandled);
    }

    private void OnStateHandled(EntityUid uid, FSDeployableItemComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;
        sprite.Color = comp.Stock <= 0 ? new Color(0.45f, 0.45f, 0.45f) : Color.White;
    }
}
