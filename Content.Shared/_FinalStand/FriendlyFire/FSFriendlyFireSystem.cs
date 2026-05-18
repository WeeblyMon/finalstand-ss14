using System.Linq;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._FinalStand.FriendlyFire;

/// <summary>
///     Blocks all damage and melee attacks between wave players (entities with FSFriendlyFireComponent).
///     Runs on both server and client so client prediction also suppresses false hit effects.
///
///     Uses FSFriendlyFireComponent as the anchor (same pattern as SharedGodmodeSystem / GodmodeComponent)
///     rather than ActorComponent so the subscription is on a fully game-owned type.
/// </summary>
public sealed class FSFriendlyFireSharedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        // Block ALL damage where origin is a wave player (melee fallback, ranged, explosions, etc.)
        SubscribeLocalEvent<FSFriendlyFireComponent, BeforeDamageChangedEvent>(OnBeforeDamage);

        // Block targeted light attacks and per-target checks inside heavy attacks
        SubscribeLocalEvent<FSFriendlyFireComponent, AttackAttemptEvent>(OnAttackAttempt);

        // Block wide melee swings where every target in the arc is a wave player
        SubscribeLocalEvent<MeleeWeaponComponent, MeleeHitEvent>(OnMeleeHit);
    }

    // uid = victim (has FSFriendlyFireComponent). Cancel if origin is also a wave player.
    private void OnBeforeDamage(EntityUid uid, FSFriendlyFireComponent _, ref BeforeDamageChangedEvent args)
    {
        if (args.Origin != null && HasComp<FSFriendlyFireComponent>(args.Origin.Value))
            args.Cancelled = true;
    }

    // uid = attacker (has FSFriendlyFireComponent). Cancel if the target is also a wave player.
    private void OnAttackAttempt(EntityUid uid, FSFriendlyFireComponent _, AttackAttemptEvent args)
    {
        if (args.Target != null && HasComp<FSFriendlyFireComponent>(args.Target.Value))
            args.Cancel();
    }

    // Fired on the weapon entity for every melee swing (light or heavy).
    // When ALL entities in the swing arc are wave players, cancel the whole hit event so
    // sounds and effects don't play. Mixed arcs (players + enemies) are handled per-entity
    // by OnAttackAttempt (heavy attacks call CanAttack per target) and OnBeforeDamage.
    private void OnMeleeHit(EntityUid uid, MeleeWeaponComponent _, MeleeHitEvent args)
    {
        if (!HasComp<FSFriendlyFireComponent>(args.User))
            return;

        if (args.HitEntities.Count > 0 && args.HitEntities.All(e => HasComp<FSFriendlyFireComponent>(e)))
            args.Handled = true;
    }
}
