using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs.Components;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSHarmHostilesSystem : EntityEffectSystem<MobStateComponent, FSHarmHostiles>
{
    [Dependency] private DamageableSystem _damageable = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<FSHarmHostiles> args)
    {
        if (HasComp<FSFriendlyFireComponent>(entity))
            return;

        _damageable.TryChangeDamage(entity.Owner, args.Effect.Damage, true);
    }
}

public sealed partial class FSHarmHostiles : EntityEffectBase<FSHarmHostiles>
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();
}
