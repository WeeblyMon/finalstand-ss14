using System.Numerics;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Damage.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Melee;

public abstract partial class SharedFSMeleeJuiceSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected SharedTransformSystem Xform = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] protected SharedMeleeWeaponSystem Melee = default!;

    // Mob movement friction on a normal tile; a push of v travels about v / friction.
    protected const float MobFriction = 20f;

    // Specials and bosses shrug off most of a stagger, so long finisher flinches can't lock them down.
    private const float SpecialFlinchScale = 0.4f;

    private readonly List<EntityUid> _expired = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSMeleeJuiceComponent, MeleeHitEvent>(OnMeleeHit, after: [typeof(FSFriendlyFireSharedSystem)]);
        SubscribeLocalEvent<FSMeleeJuiceComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<FSMeleeFlinchComponent, RefreshMovementSpeedModifiersEvent>(OnFlinchSpeed);
        SubscribeLocalEvent<FSMeleeFlinchComponent, AttackAttemptEvent>(OnFlinchAttack);
        SubscribeLocalEvent<FSMeleeJuiceComponent, ComponentShutdown>(OnWeaponShutdown);
    }

    protected abstract void OnHit(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, List<EntityUid> targets, Vector2 direction, bool heavy, bool finisher, bool lethal, MeleeHitEvent args);

    private void OnAttemptMelee(Entity<FSMeleeJuiceComponent> ent, ref AttemptMeleeEvent args)
    {
        if (!args.IsHeavyAttack || !ent.Comp.HeavyRequiresWield)
            return;

        if (TryComp<WieldableComponent>(ent, out var wield) && wield.Wielded)
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString("fs-melee-heavy-requires-wield");
    }

    private void OnMeleeHit(Entity<FSMeleeJuiceComponent> ent, ref MeleeHitEvent args)
    {
        if (args.Handled || !args.IsHit)
            return;

        var heavy = args.Direction != null;
        var targets = new List<EntityUid>();
        foreach (var hit in args.HitEntities)
        {
            if (hit != args.User && !IsAlly(args.User, hit))
                targets.Add(hit);
        }

        var finisher = !heavy && targets.Count > 0 && AdvanceCombo(args.User, ent);
        var profile = ent.Comp.Get(heavy, finisher);

        if (targets.Count == 0)
            return;

        if (!MathHelper.CloseTo(profile.DamageMultiplier, 1f))
            args.BonusDamage += args.BaseDamage * (profile.DamageMultiplier - 1f);

        var userPos = Xform.GetWorldPosition(args.User);
        var direction = args.Direction ?? Xform.GetWorldPosition(targets[0]) - userPos;
        if (direction == Vector2.Zero)
            direction = Vector2.UnitY;

        if (profile.Lunge > 0f)
            Lunge(args.User, ent, Xform.GetWorldPosition(targets[0]) - userPos, profile.Lunge);

        var damage = (args.BaseDamage + args.BonusDamage).GetTotal();
        var lethal = false;
        foreach (var target in targets)
        {
            if (!TryComp<DamageableComponent>(target, out var damageable)
                || !TryComp<MobThresholdsComponent>(target, out var thresholds)
                || !_thresholds.TryGetThresholdForState(target, MobState.Dead, out var threshold, thresholds))
                continue;

            if (damageable.TotalDamage + damage >= threshold)
            {
                lethal = true;
                break;
            }
        }

        OnHit(ent, args.User, targets, direction, heavy, finisher, lethal, args);
    }

    // Players can't damage teammates or the console, but knockback and flinch would still land on them.
    public bool IsAlly(EntityUid user, EntityUid target)
    {
        return HasComp<FSFriendlyFireComponent>(user)
            && (HasComp<FSFriendlyFireComponent>(target) || HasComp<FSPlayerDamageImmuneComponent>(target));
    }

    // A combo pointing at a deleted weapon can't be sent to clients.
    private void OnWeaponShutdown(Entity<FSMeleeJuiceComponent> ent, ref ComponentShutdown args)
    {
        if (!_net.IsServer)
            return;

        var combos = EntityQueryEnumerator<FSMeleeComboComponent>();
        while (combos.MoveNext(out var uid, out var combo))
        {
            if (combo.Weapon == ent.Owner)
                RemCompDeferred<FSMeleeComboComponent>(uid);
        }
    }

    private bool AdvanceCombo(EntityUid user, Entity<FSMeleeJuiceComponent> weapon)
    {
        if (weapon.Comp.Finisher == null)
            return false;

        var now = Timing.CurTime;
        var combo = EnsureComp<FSMeleeComboComponent>(user);
        if (combo.Weapon != weapon.Owner || now > combo.Expire)
            combo.Hits = 0;

        var finisher = combo.Hits >= weapon.Comp.ComboHits;
        combo.Weapon = weapon.Owner;
        combo.Hits = finisher ? 0 : combo.Hits + 1;
        var rate = TryComp<MeleeWeaponComponent>(weapon, out var melee) ? Melee.GetAttackRate(weapon, user, melee) : 0f;
        var cooldown = rate > 0f ? 1f / rate : 0f;
        combo.Expire = now + TimeSpan.FromSeconds(cooldown + weapon.Comp.ComboWindow);
        Dirty(user, combo);
        return finisher;
    }

    private void Lunge(EntityUid user, EntityUid weapon, Vector2 toTarget, float distance)
    {
        if (TryComp<ItemToggleComponent>(weapon, out var toggle) && !toggle.Activated
            || !TryComp<PhysicsComponent>(user, out var body))
            return;

        // Stop short of the target instead of walking into it.
        distance = Math.Min(distance, toTarget.Length() - 0.8f);
        if (distance <= 0f)
            return;

        _physics.SetLinearVelocity(user, Vector2.Normalize(toTarget) * distance * MobFriction, body: body);
    }

    private void OnFlinchSpeed(Entity<FSMeleeFlinchComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.ModifySpeed(ent.Comp.Speed);
    }

    private void OnFlinchAttack(Entity<FSMeleeFlinchComponent> ent, ref AttackAttemptEvent args)
    {
        args.Cancel();
    }

    public void Flinch(EntityUid target, float duration, float speed)
    {
        if (HasComp<FSSpecialZombieComponent>(target))
            duration *= SpecialFlinchScale;

        if (duration <= 0f)
            return;

        var end = Timing.CurTime + TimeSpan.FromSeconds(duration);
        var flinch = EnsureComp<FSMeleeFlinchComponent>(target);
        if (flinch.EndTime >= end && flinch.Speed <= speed)
            return;

        flinch.EndTime = end;
        flinch.Speed = Math.Clamp(speed, 0.1f, 1f);
        Dirty(target, flinch);
        _movement.RefreshMovementSpeedModifiers(target);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var now = Timing.CurTime;
        _expired.Clear();
        var flinches = EntityQueryEnumerator<FSMeleeFlinchComponent>();
        while (flinches.MoveNext(out var uid, out var flinch))
        {
            if (now >= flinch.EndTime)
                _expired.Add(uid);
        }

        foreach (var uid in _expired)
        {
            RemComp<FSMeleeFlinchComponent>(uid);
            _movement.RefreshMovementSpeedModifiers(uid);
        }
    }
}
