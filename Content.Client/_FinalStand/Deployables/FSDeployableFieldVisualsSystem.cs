using System.Numerics;
using Content.Shared._FinalStand.Deployables;
using Robust.Client.GameObjects;

namespace Content.Client._FinalStand.Deployables;

public sealed partial class FSDeployableFieldVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSDeployableFieldVisualsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSDeployableFieldVisualsComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnStartup(Entity<FSDeployableFieldVisualsComponent> ent, ref ComponentStartup args) => Apply(ent);

    private void OnState(Entity<FSDeployableFieldVisualsComponent> ent, ref AfterAutoHandleStateEvent args) => Apply(ent);

    private void Apply(Entity<FSDeployableFieldVisualsComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite)
            || !_sprite.LayerMapTryGet((ent, sprite), ent.Comp.LayerKey, out var index, false))
            return;

        _sprite.LayerSetScale((ent, sprite), index, new Vector2(ent.Comp.Scale, ent.Comp.Scale));
    }
}
