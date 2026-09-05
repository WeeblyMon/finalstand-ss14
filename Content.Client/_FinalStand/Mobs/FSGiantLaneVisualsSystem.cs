using System.Numerics;
using Content.Shared._FinalStand.Mobs;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._FinalStand.Mobs;

public sealed class FSGiantLaneVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSGiantLaneComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSGiantLaneComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnStartup(Entity<FSGiantLaneComponent> ent, ref ComponentStartup args) => Apply(ent);

    private void OnState(Entity<FSGiantLaneComponent> ent, ref AfterAutoHandleStateEvent args) => Apply(ent);

    private void Apply(Entity<FSGiantLaneComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetScale((ent.Owner, sprite), new Vector2(ent.Comp.Width, ent.Comp.Length));
    }
}
