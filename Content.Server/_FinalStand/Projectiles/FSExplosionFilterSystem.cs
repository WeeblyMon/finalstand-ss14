using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.Damage.Components;
using Content.Shared.Explosion;
using Content.Shared.Mobs.Components;

namespace Content.Server._FinalStand.Projectiles;

// Blocks FS explosion damage to non-mob entities (wires, tables, windows) and friendly players.
// FS explosion types opt in by being listed in FsExplosionTypes.
public sealed class FSExplosionFilterSystem : EntitySystem
{
    private static readonly HashSet<string> FsExplosionTypes = new()
    {
        "FSGrenadeExplosion",
        "FSRocketExplosion",
    };

    private EntityQuery<MobStateComponent> _mobQuery;
    private EntityQuery<FSFriendlyFireComponent> _ffQuery;

    public override void Initialize()
    {
        base.Initialize();
        _mobQuery = GetEntityQuery<MobStateComponent>();
        _ffQuery = GetEntityQuery<FSFriendlyFireComponent>();
        SubscribeLocalEvent<DamageableComponent, GetExplosionResistanceEvent>(OnGetResistance);
    }

    private void OnGetResistance(EntityUid uid, DamageableComponent _, ref GetExplosionResistanceEvent args)
    {
        if (!FsExplosionTypes.Contains(args.ExplosionPrototype))
            return;

        if (!_mobQuery.HasComponent(uid) || _ffQuery.HasComponent(uid))
            args.DamageCoefficient = 0f;
    }
}
