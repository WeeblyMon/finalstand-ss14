using System.Numerics;
using Content.Shared._FinalStand.Grenades;
using Robust.Client.GameObjects;

namespace Content.Client._FinalStand.Grenades;

public sealed partial class FSSingularityVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSSingularityComponent, AfterAutoHandleStateEvent>(OnStateHandled);
    }

    private void OnStateHandled(EntityUid uid, FSSingularityComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (comp.VisualRadius <= 0f || !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var scale = comp.Radius / comp.VisualRadius;
        _sprite.SetScale((uid, sprite), new Vector2(scale, scale));
    }
}
