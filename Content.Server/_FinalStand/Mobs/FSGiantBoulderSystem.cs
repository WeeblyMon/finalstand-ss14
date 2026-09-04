using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;

namespace Content.Server._FinalStand.Mobs;

public sealed class FSGiantBoulderSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSGiantBoulderComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(Entity<FSGiantBoulderComponent> ent, ref StartCollideEvent args)
    {
        var other = args.OtherEntity;

        if (HasComp<MobStateComponent>(other) || HasComp<ProjectileComponent>(other))
            return;

        if (!HasComp<DamageableComponent>(other) || !Transform(other).Anchored)
            return;

        // deleteOnCollide is off, so the same wall can raise several contacts before the boulder dies
        if (!ent.Comp.Struck.Add(other))
            return;

        Spawn(ent.Comp.ImpactEffect, Transform(other).Coordinates);
        _audio.PlayPvs(ent.Comp.ImpactSound, other);

        // Reinforced walls carry this marker and are what keeps the station sealed, so the rock
        // stops dead on them rather than opening the hull.
        if (HasComp<FSPlayerDamageImmuneComponent>(other))
        {
            Spawn(ent.Comp.ImpactEffect, Transform(ent).Coordinates);
            QueueDel(ent);
            return;
        }

        var damage = new DamageSpecifier();
        damage.DamageDict["Structural"] = FixedPoint2.New(ent.Comp.StructuralDamage);
        _damageable.TryChangeDamage(other, damage, origin: ent.Owner);

        var broke = TerminatingOrDeleted(other) || EntityManager.IsQueuedForDeletion(other);
        ent.Comp.StructurePierce--;

        if (broke && ent.Comp.StructurePierce > 0)
            return;

        Spawn(ent.Comp.ImpactEffect, Transform(ent).Coordinates);
        QueueDel(ent);
    }
}
