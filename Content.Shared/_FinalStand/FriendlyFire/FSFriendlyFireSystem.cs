using System.Linq;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Physics.Events;

namespace Content.Shared._FinalStand.FriendlyFire;

public sealed class FSFriendlyFireSharedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSFriendlyFireComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<FSPlayerDamageImmuneComponent, BeforeDamageChangedEvent>(OnBeforeStructureDamage);
        SubscribeLocalEvent<FSFriendlyFireComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<FSFriendlyFireComponent, PreventCollideEvent>(OnPlayerPreventCollide);
        SubscribeLocalEvent<MeleeWeaponComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnBeforeDamage(EntityUid uid, FSFriendlyFireComponent _, ref BeforeDamageChangedEvent args)
    {
        if (args.Damage.GetTotal() < 0)
            return;
        if (args.Origin != null && HasComp<FSFriendlyFireComponent>(args.Origin.Value))
            args.Cancelled = true;
    }

    private void OnBeforeStructureDamage(EntityUid uid, FSPlayerDamageImmuneComponent _, ref BeforeDamageChangedEvent args)
    {
        if (args.Origin != null && HasComp<FSFriendlyFireComponent>(args.Origin.Value))
            args.Cancelled = true;
    }

    private void OnPlayerPreventCollide(EntityUid uid, FSFriendlyFireComponent _, ref PreventCollideEvent args)
    {
        if (!TryComp<ProjectileComponent>(args.OtherEntity, out var proj)) return;
        if (proj.Shooter == null) return;
        if (HasComp<FSFriendlyFireComponent>(proj.Shooter.Value))
            args.Cancelled = true;
    }

    private void OnAttackAttempt(EntityUid uid, FSFriendlyFireComponent _, AttackAttemptEvent args)
    {
        if (args.Target != null && HasComp<FSFriendlyFireComponent>(args.Target.Value))
            args.Cancel();
    }

    // strips non-mob entities so melee can't damage structures; if ALL remaining are wave players, cancels the hit
    private void OnMeleeHit(EntityUid uid, MeleeWeaponComponent _, MeleeHitEvent args)
    {
        if (args.HitEntities is List<EntityUid> mutableList)
            mutableList.RemoveAll(e => !HasComp<MobStateComponent>(e));

        if (!HasComp<FSFriendlyFireComponent>(args.User))
            return;

        if (args.HitEntities.Count > 0 && args.HitEntities.All(e => HasComp<FSFriendlyFireComponent>(e)))
            args.Handled = true;
    }
}
