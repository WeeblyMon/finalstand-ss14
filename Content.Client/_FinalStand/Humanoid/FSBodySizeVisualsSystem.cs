using Content.Shared._FinalStand.Humanoid;
using Robust.Client.GameObjects;

namespace Content.Client._FinalStand.Humanoid;

public sealed partial class FSBodySizeVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSBodySizeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSBodySizeComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<FSBodySizeComponent, FSBodySizeChangedEvent>(OnChanged);
    }

    private void OnStartup(Entity<FSBodySizeComponent> ent, ref ComponentStartup args)
    {
        Apply(ent);
    }

    private void OnState(Entity<FSBodySizeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Apply(ent);
    }

    private void OnChanged(Entity<FSBodySizeComponent> ent, ref FSBodySizeChangedEvent args)
    {
        Apply(ent);
    }

    private void Apply(Entity<FSBodySizeComponent> ent)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetScale((ent, sprite), ent.Comp.Scale);
    }
}
