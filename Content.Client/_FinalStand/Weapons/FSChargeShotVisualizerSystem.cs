using System.Numerics;
using Content.Shared._FinalStand.Weapons;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._FinalStand.Weapons;

// scales a charged pellet to match how long its shot was held
public sealed class FSChargeShotVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSChargeShotPelletComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSChargeShotPelletComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnStartup(Entity<FSChargeShotPelletComponent> ent, ref ComponentStartup args) => Apply(ent);

    private void OnState(Entity<FSChargeShotPelletComponent> ent, ref AfterAutoHandleStateEvent args) => Apply(ent);

    private void Apply(Entity<FSChargeShotPelletComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetScale((ent.Owner, sprite), new Vector2(ent.Comp.Scale, ent.Comp.Scale));
    }
}
