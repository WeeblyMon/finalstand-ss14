using Content.Server.Destructible;
using Content.Shared._FinalStand.Repair;

namespace Content.Server._FinalStand.Repair;

public sealed partial class FSStructureHealthSystem : EntitySystem
{
    [Dependency] private DestructibleSystem _destructible = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSStructureHealthComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<FSStructureHealthComponent> ent, ref MapInitEvent args)
    {
        if (!_destructible.TryGetDestroyedAt(ent.Owner, out var max))
            return;

        ent.Comp.Max = max.Value.Float();
        Dirty(ent);
    }
}
