using Content.Server.Destructible;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Damage.Components;

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
        Refresh(ent.AsNullable());
    }

    public void Refresh(Entity<FSStructureHealthComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !_destructible.TryGetDestroyedAt(ent.Owner, out var max))
            return;

        ent.Comp.Max = max.Value.Float();
        Dirty(ent);
    }

    public float? Fraction(EntityUid uid)
    {
        if (!TryComp<FSStructureHealthComponent>(uid, out var health) || health.Max <= 0f
            || !TryComp<DamageableComponent>(uid, out var damageable))
            return null;

        return FSStructureHealth.Fraction(health, damageable);
    }
}
