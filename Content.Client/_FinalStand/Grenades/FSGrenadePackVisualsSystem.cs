using Content.Shared._FinalStand.Grenades;
using Robust.Client.GameObjects;
using Robust.Shared.Analyzers;

namespace Content.Client._FinalStand.Grenades;

public sealed partial class FSGrenadePackVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSGrenadePackComponent, AfterAutoHandleStateEvent>(OnStateHandled);
    }

    private void OnStateHandled(EntityUid uid, FSGrenadePackComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;
        sprite.Color = comp.Stock <= 0 ? new Color(0.45f, 0.45f, 0.45f) : Color.White;

        if (comp.IsSingularity && comp.SingularitySprite != null)
            _sprite.LayerSetSprite((uid, sprite), 0, comp.SingularitySprite);
    }
}
