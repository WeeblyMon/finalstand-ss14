using System.Numerics;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.ActionBlocker;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio.Systems;
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
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedAudioSystem _chargeAudio = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;

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
        SubscribeAllEvent<FSMeleeChargeEvent>(OnChargeEvent);
        SubscribeLocalEvent<FSMeleeChargeComponent, AttemptMeleeEvent>(OnChargeAttempt);
        SubscribeLocalEvent<FSMeleeChargeComponent, GotUnequippedHandEvent>(OnChargeUnequipped);
        SubscribeLocalEvent<FSMeleeChargeComponent, HandDeselectedEvent>(OnChargeDeselected);
    }

    // Heavy profiles grow with the charge behind them.
    public FSMeleeJuiceProfile Profile(Entity<FSMeleeJuiceComponent> weapon, bool heavy, bool finisher = false)
    {
        var profile = weapon.Comp.Get(heavy, finisher);
        return heavy && TryComp<FSMeleeChargeComponent>(weapon, out var charge) ? charge.Apply(profile) : profile;
    }

    private void OnChargeEvent(FSMeleeChargeEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user
            || !TryGetEntity(ev.Weapon, out var weapon)
            || !TryComp<FSMeleeChargeComponent>(weapon, out var charge)
            || !Melee.TryGetWeapon(user, out var held, out var melee)
            || held != weapon)
            return;

        if (!ev.Start)
        {
            StopCharge((weapon.Value, charge));
            return;
        }

        if (charge.ChargeStart != null
            || melee.NextAttack > Timing.CurTime
            || !_combat.IsInCombatMode(user)
            || !_blocker.CanAttack(user, weapon: (weapon.Value, melee)))
            return;

        charge.ChargeStart = Timing.CurTime;
        Dirty(weapon.Value, charge);

        if (_net.IsServer)
        {
            charge.ChargeStream = _chargeAudio.Stop(charge.ChargeStream);
            charge.ChargeStream = _chargeAudio.PlayPvs(charge.ChargeSound, user)?.Entity;
        }
    }

    // Any swing ends the wind-up; a heavy one keeps how far it got.
    private void OnChargeAttempt(Entity<FSMeleeChargeComponent> ent, ref AttemptMeleeEvent args)
    {
        if (args.IsHeavyAttack)
        {
            ent.Comp.LastCharge = ent.Comp.ChargeAt(Timing.CurTime);
            Dirty(ent);
        }

        StopCharge(ent);
    }

    private void OnChargeUnequipped(EntityUid uid, FSMeleeChargeComponent charge, GotUnequippedHandEvent args)
    {
        StopCharge((uid, charge));
    }

    private void OnChargeDeselected(EntityUid uid, FSMeleeChargeComponent charge, HandDeselectedEvent args)
    {
        StopCharge((uid, charge));
    }

    private void StopCharge(Entity<FSMeleeChargeComponent> ent)
    {
        if (_net.IsServer)
            ent.Comp.ChargeStream = _chargeAudio.Stop(ent.Comp.ChargeStream);

        if (ent.Comp.ChargeStart == null)
            return;

        ent.Comp.ChargeStart = null;
        Dirty(ent);
    }

    protected abstract void OnHit(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, List<EntityUid> targets, Vector2 direction, bool heavy, bool finisher, bool lethal, MeleeHitEvent args);

    protected virtual void OnHeavyMiss(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, Vector2 aim, FSMeleeJuiceProfile profile, DamageSpecifier damage)
    {
    }

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
        var profile = Profile(ent, heavy, finisher);

        if (heavy)
        {
            Recover(args.User, ent);
            if (profile.MaxTargets > 0 && targets.Count > profile.MaxTargets)
                KeepClosest(args, targets, profile.MaxTargets);

            if (profile.Dash > 0f && args.Direction is { } aim && aim != Vector2.Zero)
                Lunge(args.User, ent, ClosestOffset(args.User, targets) ?? aim, profile.Dash);

            if (targets.Count == 0 && args.Direction is { } missed && missed != Vector2.Zero)
                OnHeavyMiss(ent, args.User, missed, profile, args.BaseDamage + args.BonusDamage);
        }

        if (targets.Count == 0)
            return;

        if (!MathHelper.CloseTo(profile.DamageMultiplier, 1f))
            args.BonusDamage += args.BaseDamage * (profile.DamageMultiplier - 1f);

        if (heavy && TryComp<FSMeleeChargeComponent>(ent, out var charge) && charge.WasFull)
            _chargeAudio.PlayPredicted(charge.FullHitSound, targets[0], args.User);

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

    // Heavy attacks trade the next swing for their reach, so they hold off the weapon's next attack.
    private void Recover(EntityUid user, Entity<FSMeleeJuiceComponent> weapon)
    {
        if (weapon.Comp.HeavyRecovery <= 0f || !TryComp<MeleeWeaponComponent>(weapon, out var melee))
            return;

        var rate = Melee.GetAttackRate(weapon, user, melee);
        if (rate <= 0f)
            return;

        var next = Timing.CurTime + TimeSpan.FromSeconds(weapon.Comp.HeavyRecovery * melee.AttackRate / rate);
        if (next <= melee.NextAttack)
            return;

        melee.NextAttack = next;
        DirtyField(weapon, melee, nameof(MeleeWeaponComponent.NextAttack));
    }

    private Vector2? ClosestOffset(EntityUid user, List<EntityUid> targets)
    {
        var userPos = Xform.GetWorldPosition(user);
        Vector2? best = null;
        foreach (var target in targets)
        {
            var offset = Xform.GetWorldPosition(target) - userPos;
            if (best == null || offset.LengthSquared() < best.Value.LengthSquared())
                best = offset;
        }

        return best;
    }

    // Drops all but the closest mobs from the swing so it lands on them alone.
    private void KeepClosest(MeleeHitEvent args, List<EntityUid> targets, int count)
    {
        var userPos = Xform.GetWorldPosition(args.User);
        targets.Sort((a, b) => (Xform.GetWorldPosition(a) - userPos).LengthSquared().CompareTo((Xform.GetWorldPosition(b) - userPos).LengthSquared()));
        var dropped = targets.GetRange(count, targets.Count - count);
        targets.RemoveRange(count, targets.Count - count);

        if (args.HitEntities is List<EntityUid> hits)
            hits.RemoveAll(dropped.Contains);
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
