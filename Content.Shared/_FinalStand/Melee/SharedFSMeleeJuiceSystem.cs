using System.Numerics;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Sprint;
using Content.Shared.ActionBlocker;
using Content.Shared.Body.Components;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
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
    [Dependency] private EntityLookupSystem _sweepLookup = default!;
    [Dependency] private SharedInteractionSystem _sweepReach = default!;
    [Dependency] private SharedPopupSystem _shotPopup = default!;

    // Mob movement friction on a normal tile; a push of v travels about v / friction.
    protected const float MobFriction = 20f;

    // Specials and bosses shrug off most of a stagger, so long finisher flinches can't lock them down.
    private const float SpecialFlinchScale = 0.4f;

    // Flinches this long leave a zombie open to an execution once it is badly hurt.
    private const float StaggerFlinch = 0.35f;
    private static readonly TimeSpan StaggerGrace = TimeSpan.FromSeconds(1.2);
    private const float ExecutionHitstop = 0.14f;
    private const float ExecutionZoom = 0.12f;

    private const float SprintMinSpeed = 2f;
    private static readonly TimeSpan SprintAttackCooldown = TimeSpan.FromSeconds(1.2);
    private const float SprintDamage = 1.25f;
    private const float SprintKnockback = 1.75f;
    private const float SprintFlinch = 1.75f;
    private const float SprintLunge = 1.1f;
    private const float RevPitchStep = 0.05f;
    private const float WallMargin = 0.5f;
    private const float RevDumpForce = 0.15f;
    private const float SweepPickRadius = 1.25f;
    private const float SweepThrough = 0.9f;
    private const float SweepOverrun = 0.5f;
    private const float SweepRecovery = 0.3f;
    private const float SweepHitRadius = 0.75f;
    private const float SweepPassDamage = 0.6f;
    private const float SweepRetry = 0.15f;
    private static readonly TimeSpan SweepPassTime = TimeSpan.FromSeconds(0.1);
    private static readonly TimeSpan SweepGuardTime = TimeSpan.FromSeconds(0.3);
    private const float SweepDashSpeed = 9f;
    private const float SweepDashMin = 0.2f;
    private const float SweepDashMax = 0.6f;
    private const float DashExitSpeed = 4f;
    private const float GuardDamage = 0.5f;
    private const float RevDumpTrauma = 0.08f;
    private static readonly TimeSpan WhirlInterval = TimeSpan.FromSeconds(0.45);
    private const float WhirlHealthCost = 4f;
    private const float WhirlHealthFloor = 0.85f;

    private readonly List<EntityUid> _expired = new();
    private readonly HashSet<Entity<MobStateComponent>> _passing = new();
    private readonly List<Entity<FSMeleeDashComponent, PhysicsComponent>> _dashing = new();

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
        SubscribeLocalEvent<FSMeleeSweepComponent, PreventCollideEvent>(OnSweepCollide);
        SubscribeLocalEvent<FSMeleeDashComponent, RefreshFrictionModifiersEvent>(OnDashFriction);
        SubscribeLocalEvent<FSMeleeDashComponent, AttemptMobCollideEvent>(OnDashMobCollide);
        SubscribeLocalEvent<FSMeleeDashComponent, AttemptMobTargetCollideEvent>(OnDashMobTargetCollide);
        SubscribeLocalEvent<FSMeleeGuardComponent, DamageModifyEvent>(OnGuardDamage);
    }

    // Heavy profiles grow with the charge behind them.
    public FSMeleeJuiceProfile Profile(Entity<FSMeleeJuiceComponent> weapon, bool heavy, bool finisher = false)
    {
        var profile = weapon.Comp.Get(heavy, finisher);
        if (!heavy)
            return profile;

        if (TryComp<FSMeleeChargeComponent>(weapon, out var charge))
            profile = charge.Apply(profile);

        if (profile.Dash > 0f && TryComp<FSWeaponUpgradeStateComponent>(weapon, out var upgrades) && upgrades.DashBonus > 0f)
        {
            profile = profile.Copy();
            profile.Dash *= 1f + upgrades.DashBonus;
            profile.DamageMultiplier *= 1f + upgrades.DashBonus;
        }

        // Grand Slam: a full charge sends its target twice as far and its crashes land in full.
        if (profile.FullCharge && Signature(weapon, FSMeleeSignature.GrandSlam) > 0f)
        {
            profile = profile.Copy();
            profile.Knockback *= GrandSlamForce;
            profile.Launch = 1f;
        }

        return profile;
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

        // Unstoppable: the wind-up takes half damage.
        if (Signature(weapon.Value, FSMeleeSignature.Unstoppable) > 0f)
        {
            var guard = EnsureComp<FSMeleeGuardComponent>(user);
            guard.Charging = weapon;
            Dirty(user, guard);
        }

        // Running Start: a wind-up begun at a sprint starts half full.
        if (Signature(weapon.Value, FSMeleeSignature.RunningStart) > 0f && TryComp<FSSprintComponent>(user, out var sprint) && sprint.IsSprinting)
            charge.ChargeStart -= TimeSpan.FromSeconds(charge.MaxChargeTime * RunningStartCharge);

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

    protected abstract void OnHit(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, List<EntityUid> targets, Vector2 direction, bool heavy, bool finisher, bool lethal, FSMeleeJuiceProfile profile, MeleeHitEvent args);

    protected virtual void OnHeavyShot(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, Vector2 aim)
    {
    }

    protected virtual void OnHeavyMiss(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, Vector2 aim, FSMeleeJuiceProfile profile, DamageSpecifier damage)
    {
    }

    private void OnAttemptMelee(Entity<FSMeleeJuiceComponent> ent, ref AttemptMeleeEvent args)
    {
        if (!args.IsHeavyAttack)
            return;

        if (!HeavyReady(args.User, ent))
        {
            args.Cancelled = true;
            return;
        }

        if (!ent.Comp.HeavyRequiresWield)
            return;

        if (TryComp<WieldableComponent>(ent, out var wield) && wield.Wielded)
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString("fs-melee-heavy-requires-wield");
    }

    private void OnMeleeHit(Entity<FSMeleeJuiceComponent> ent, ref MeleeHitEvent args)
    {
        var heavy = args.Direction != null;

        // Shots and sweeps don't use the swing's arc, so a teammate standing in it mustn't stop them.
        if (heavy && args.IsHit && ent.Comp.HeavyStyle == FSMeleeHeavyStyle.Shot)
        {
            args.Handled = true;
            if (args.Direction is { } aim && aim != Vector2.Zero && TakeShot(ent, args.User, aim))
                OnHeavyShot(ent, args.User, aim);
            return;
        }

        if (heavy && args.IsHit && ent.Comp.HeavyStyle == FSMeleeHeavyStyle.Sweep)
        {
            args.Handled = true;
            if (args.Direction is { } cursor)
                Sweep(ent, args.User, cursor, args);
            return;
        }

        if (!args.IsHit)
            return;

        // Even a swing that only caught teammates keeps the heavy's rhythm, and Whirlwind's cost.
        if (heavy)
            Recover(args.User, ent);

        if (args.Handled)
            return;

        var targets = new List<EntityUid>();
        foreach (var hit in args.HitEntities)
        {
            if (hit != args.User && !IsAlly(args.User, hit) && !(TryComp<MobStateComponent>(hit, out var state) && state.CurrentState == MobState.Dead))
                targets.Add(hit);
        }

        // Walls, crates and the like still feel the hit, but only living mobs build combos.
        var living = CountLiving(targets);
        var finisher = !heavy && living > 0 && AdvanceCombo(args.User, ent);
        var profile = Profile(ent, heavy, finisher);

        if (heavy)
        {
            if (profile.MaxTargets > 0 && targets.Count > profile.MaxTargets)
                KeepClosest(args, targets, profile.MaxTargets);

            if (profile.Dash > 0f && args.Direction is { } aim && aim != Vector2.Zero)
                Lunge(args.User, ent, ClosestOffset(args.User, targets) ?? aim, profile.Dash);

            if (targets.Count == 0 && args.Direction is { } missed && missed != Vector2.Zero)
                OnHeavyMiss(ent, args.User, missed, profile, args.BaseDamage + args.BonusDamage);
        }

        if (!heavy)
            TryTempest(ent, args.User, targets, args);

        if (targets.Count == 0)
            return;

        if (living > 0)
            profile = Embellish(ent, args.User, targets, profile, heavy, args);

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
        var lethal = targets.Exists(t => WouldKill(t, damage));

        OnHit(ent, args.User, targets, direction, heavy, finisher, lethal || profile.Execution, profile, args);
    }

    public int CountLiving(List<EntityUid> targets)
    {
        var count = 0;
        foreach (var target in targets)
        {
            if (TryComp<MobStateComponent>(target, out var state) && state.CurrentState != MobState.Dead)
                count++;
        }

        return count;
    }

    // Rev, sprint and execution only change the copy this one swing uses.
    private FSMeleeJuiceProfile Embellish(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, List<EntityUid> targets, FSMeleeJuiceProfile profile, bool heavy, MeleeHitEvent args)
    {
        var rev = heavy ? SpendRev(weapon) : Rev(weapon);
        var sprint = !heavy && SprintAttack(user, weapon);
        var execution = targets.Exists(t => CanExecute(t, weapon));
        var crit = SwingCrits(weapon, profile, heavy, rev);
        var brace = !heavy && Braced(user, weapon);
        if (rev == 0 && !sprint && !execution && !crit && !brace)
            return profile;

        profile = profile.Copy();
        profile.Crit = crit;
        if (brace)
        {
            profile.Pierce = Math.Max(profile.Pierce, BracePierce);
            profile.Knockback *= BraceForce;
            profile.FlinchDuration *= BraceForce;
        }

        if (rev > 0)
        {
            profile.Rev = rev;
            if (heavy)
            {
                profile.DamageMultiplier *= 1f + weapon.Comp.RevDumpStep * rev;
                profile.Knockback *= 1f + RevDumpForce * rev;
                profile.FlinchDuration *= 1f + RevDumpForce * rev;
                profile.Trauma = Math.Min(1f, profile.Trauma + RevDumpTrauma * rev);
                if (rev >= RevCap(weapon) / 2)
                    _chargeAudio.PlayPredicted(weapon.Comp.RevDumpSound, weapon, user);
            }
            else
            {
                profile.DamageMultiplier *= 1f + weapon.Comp.RevStep * rev;
            }

            if (TryComp<MeleeWeaponComponent>(weapon, out var melee) && melee.HitSound is { } sound)
                args.HitSoundOverride = Pitched(sound, 1f + RevPitchStep * rev);
        }

        if (sprint)
        {
            profile.Sprint = true;
            profile.DamageMultiplier *= SprintDamage;
            profile.Knockback *= SprintKnockback;
            profile.FlinchDuration *= SprintFlinch;
            profile.Lunge = Math.Max(profile.Lunge, SprintLunge);
            profile.Trauma = Math.Min(1f, profile.Trauma + 0.15f);
        }

        if (execution)
        {
            CoupDeGrace(user, weapon);
            profile.Execution = true;
            profile.Hitstop += ExecutionHitstop;
            profile.KillZoom = Math.Max(profile.KillZoom, ExecutionZoom);
            profile.Trauma = Math.Min(1f, profile.Trauma + 0.3f);
        }

        return profile;
    }

    private static SoundSpecifier? Pitched(SoundSpecifier sound, float pitch)
    {
        return sound switch
        {
            SoundPathSpecifier path => new SoundPathSpecifier(path.Path, path.Params.WithPitchScale(path.Params.Pitch * pitch)),
            SoundCollectionSpecifier { Collection: { } collection } set => new SoundCollectionSpecifier(collection, set.Params.WithPitchScale(set.Params.Pitch * pitch)),
            _ => null,
        };
    }

    // Every hit in a row climbs the rev, whichever zombie it bites; a pause starts it over.
    private int Rev(Entity<FSMeleeJuiceComponent> weapon)
    {
        if (weapon.Comp.RevStep <= 0f)
            return 0;

        var now = Timing.CurTime;
        var rev = EnsureComp<FSMeleeRevComponent>(weapon);
        var climbing = now - rev.Last <= TimeSpan.FromSeconds(weapon.Comp.RevWindow);
        rev.Stacks = climbing ? Math.Min(rev.Stacks + 1, RevCap(weapon)) : 1;
        rev.Last = now;
        Dirty(weapon, rev);
        return rev.Stacks;
    }

    // Empties the rev into one heavy, if the grind that built it was recent enough.
    private int SpendRev(Entity<FSMeleeJuiceComponent> weapon)
    {
        if (weapon.Comp.RevDumpStep <= 0f || !TryComp<FSMeleeRevComponent>(weapon, out var rev) || rev.Stacks == 0)
            return 0;

        var stacks = Timing.CurTime - rev.Last <= TimeSpan.FromSeconds(weapon.Comp.RevDumpWindow) ? rev.Stacks : 0;
        rev.Stacks = 0;
        Dirty(weapon, rev);
        return stacks;
    }

    private bool SprintAttack(EntityUid user, Entity<FSMeleeJuiceComponent> weapon)
    {
        if (!weapon.Comp.SprintAttack
            || !TryComp<FSSprintComponent>(user, out var sprint)
            || !sprint.IsSprinting
            || sprint.IsExhausted
            || !TryComp<PhysicsComponent>(user, out var body)
            || body.LinearVelocity.Length() < SprintMinSpeed)
            return false;

        var now = Timing.CurTime;
        var combo = EnsureComp<FSMeleeComboComponent>(user);
        if (now < combo.NextSprintAttack)
            return false;

        combo.NextSprintAttack = now + SprintAttackCooldown;
        Dirty(user, combo);
        OnSprintAttack(user);
        return true;
    }

    protected virtual void OnSprintAttack(EntityUid user)
    {
    }

    // A staggered ordinary zombie that is nearly dead dies to the next hit. Hemorrhage counts bleeding as staggered.
    public bool CanExecute(EntityUid target, Entity<FSMeleeJuiceComponent> weapon)
    {
        var open = TryComp<FSMeleeStaggeredComponent>(target, out var staggered) && Timing.CurTime < staggered.Until
            || Signature(weapon, FSMeleeSignature.Hemorrhage) > 0f
                && !HasComp<FSSpecialZombieComponent>(target)
                && TryComp<BloodstreamComponent>(target, out var blood) && blood.BleedAmount > 0f;

        return open && HealthLost(target) is { } lost && lost >= 1f - weapon.Comp.ExecutionHealth && lost < 1f;
    }

    // Fraction of the way to dead, or null for anything that can't die.
    public float? HealthLost(EntityUid target)
    {
        if (!TryComp<DamageableComponent>(target, out var damageable) || DeadThreshold(target) is not { } threshold)
            return null;

        return (float) damageable.TotalDamage / threshold;
    }

    protected float? DeadThreshold(EntityUid target)
    {
        if (!TryComp<MobThresholdsComponent>(target, out var thresholds)
            || !_thresholds.TryGetThresholdForState(target, MobState.Dead, out var threshold, thresholds)
            || threshold <= 0)
            return null;

        return threshold.Value.Float();
    }

    // Sweeping Blade: dash through the enemy nearest the cursor, cutting what the dash reaches, harder the more sweeps are chained.
    private void Sweep(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, Vector2 cursor, MeleeHitEvent args)
    {
        if (TryComp<ItemToggleComponent>(weapon, out var toggle) && !toggle.Activated)
            return;

        var now = Timing.CurTime;
        var reach = 1f + (TryComp<FSWeaponUpgradeStateComponent>(weapon, out var upgrades) ? upgrades.DashBonus : 0f);
        var range = weapon.Comp.SweepRange * reach;

        var sweep = EnsureComp<FSMeleeSweepComponent>(user);
        foreach (var (marked, mark) in sweep.Marked)
        {
            if (mark.Until <= now)
                sweep.Marked.Remove(marked);
        }

        var target = SweepTarget(user, sweep, Xform.GetMapCoordinates(user), cursor, range, now);
        var offset = target is { } aimed ? Xform.GetWorldPosition(aimed) - Xform.GetWorldPosition(user) : Vector2.Zero;
        var dir = offset == Vector2.Zero ? Vector2.Normalize(cursor) : Vector2.Normalize(offset);

        // Ends just clear of the far side of the target instead of flying on into whatever's behind it.
        var length = Math.Min(offset.Length() + SweepThrough, range + SweepOverrun);
        var dashing = target == null ? TimeSpan.Zero : SweepDash(user, dir, length);
        if (TryComp<MeleeWeaponComponent>(weapon, out var melee))
        {
            melee.NextAttack = now + TimeSpan.FromSeconds(dashing == TimeSpan.Zero ? SweepRetry : SweepRecovery);
            DirtyField(weapon, melee, nameof(MeleeWeaponComponent.NextAttack));
        }

        // Nothing to sweep through, or a wall right in the way: no dash, and only a moment before trying again.
        if (target == null || dashing == TimeSpan.Zero)
            return;

        Guard(user, now + dashing + SweepGuardTime);

        var chained = now - sweep.Last <= TimeSpan.FromSeconds(weapon.Comp.SweepStackWindow) ? sweep.Stacks : 0;
        sweep.Stacks = Math.Min(chained + 1, weapon.Comp.SweepMaxStacks);
        sweep.Last = now;
        sweep.Landed = now + dashing;
        sweep.NextSweep = now + TimeSpan.FromSeconds(weapon.Comp.SweepCooldown);
        sweep.Marked[target.Value] = (now, now + TimeSpan.FromSeconds(weapon.Comp.SweepMarkTime));
        sweep.PassUntil = now + dashing + SweepPassTime;
        sweep.TempestUsed = false;
        Dirty(user, sweep);

        // The dash already moves the camera; shaking, zooming or freezing it on top feels like lag.
        var profile = Profile(weapon, true).Copy();
        profile.DamageMultiplier *= reach * (1f + weapon.Comp.SweepStackBonus * chained);
        profile.Trauma = 0f;
        profile.Hitstop = 0f;
        profile.KillHitstopBonus = 0f;
        profile.KillZoom = 0f;
        profile.Crit = weapon.Comp.CritWhen == FSMeleeCritWhen.SweepChain && sweep.Stacks >= weapon.Comp.SweepMaxStacks;
        args.BonusDamage += args.BaseDamage * (profile.DamageMultiplier - 1f);

        // The cut lands as the blade reaches each zombie, so a dash stopped by a wall cuts nothing past it.
        if (!TryComp<FSMeleeDashComponent>(user, out var dash))
            return;

        dash.Weapon = weapon;
        dash.Primary = target;
        dash.Damage = args.BaseDamage + args.BonusDamage;
        dash.Profile = profile;
        dash.Hit.Clear();
        SweepPass((user, dash));
    }

    // Cuts each mob the dashing body reaches, once per dash.
    private void SweepPass(Entity<FSMeleeDashComponent> dash)
    {
        if (dash.Comp.Weapon is not { } weaponUid
            || !TryComp<FSMeleeJuiceComponent>(weaponUid, out var juice)
            || dash.Comp.Damage is not { } damage
            || dash.Comp.Profile is not { } profile)
            return;

        var user = dash.Owner;
        var dir = dash.Comp.Velocity == Vector2.Zero ? Vector2.UnitY : Vector2.Normalize(dash.Comp.Velocity);
        _passing.Clear();
        _sweepLookup.GetEntitiesInRange(Xform.GetMapCoordinates(user), SweepHitRadius, _passing);
        foreach (var mob in _passing)
        {
            if (mob.Owner == user
                || mob.Comp.CurrentState == MobState.Dead
                || dash.Comp.Hit.Contains(mob)
                || IsAlly(user, mob)
                || !_sweepReach.InRangeUnobstructed(user, mob.Owner, SweepHitRadius + 0.5f))
                continue;

            dash.Comp.Hit.Add(mob);
            var primary = mob.Owner == dash.Comp.Primary;
            var cut = primary ? damage : damage * SweepPassDamage;
            var hitProfile = profile.Copy();
            hitProfile.Crit = primary && profile.Crit;
            hitProfile.Execution = Signature(weaponUid, FSMeleeSignature.LastBreath) > 0f && Airborne(mob) && !HasComp<FSSpecialZombieComponent>(mob);

            var targets = new List<EntityUid> { mob };
            var hit = new MeleeHitEvent(targets, user, weaponUid, cut, dir);
            OnHit((weaponUid, juice), user, targets, dir, true, false, hitProfile.Execution || WouldKill(mob, cut.GetTotal()), hitProfile, hit);
            OnSweepHit(user, mob, cut, hitProfile.Execution);
        }
    }

    // Whether the weapon's heavy can go now; a sweep on cooldown doesn't swing at all.
    public bool HeavyReady(EntityUid user, Entity<FSMeleeJuiceComponent> weapon)
    {
        return weapon.Comp.HeavyStyle != FSMeleeHeavyStyle.Sweep
            || !TryComp<FSMeleeSweepComponent>(user, out var sweep)
            || Timing.CurTime >= sweep.NextSweep;
    }

    private bool WouldKill(EntityUid target, FixedPoint2 damage)
    {
        return TryComp<DamageableComponent>(target, out var damageable)
            && TryComp<MobThresholdsComponent>(target, out var thresholds)
            && _thresholds.TryGetThresholdForState(target, MobState.Dead, out var threshold, thresholds)
            && damageable.TotalDamage + damage >= threshold;
    }

    private EntityUid? SweepTarget(EntityUid user, FSMeleeSweepComponent sweep, MapCoordinates from, Vector2 cursor, float range, TimeSpan now)
    {
        var aim = from.Position + cursor;
        EntityUid? best = null;
        var bestDistance = SweepPickRadius;
        var mobs = new HashSet<Entity<MobStateComponent>>();
        _sweepLookup.GetEntitiesInRange(from, range, mobs);
        foreach (var mob in mobs)
        {
            if (mob.Owner == user
                || mob.Comp.CurrentState == MobState.Dead
                || IsAlly(user, mob)
                || sweep.Marked.TryGetValue(mob, out var mark) && mark.Until > now && mark.At < now
                || !_sweepReach.InRangeUnobstructed(user, mob.Owner, range + 0.5f))
                continue;

            var distance = (Xform.GetWorldPosition(mob) - aim).Length();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = mob;
            }
        }

        return best;
    }

    protected virtual void OnSweepHit(EntityUid user, EntityUid target, DamageSpecifier damage, bool lastBreath)
    {
    }

    private void OnSweepCollide(Entity<FSMeleeSweepComponent> ent, ref PreventCollideEvent args)
    {
        if (Timing.CurTime < ent.Comp.PassUntil && HasComp<MobStateComponent>(args.OtherEntity))
            args.Cancelled = true;
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
        if (!TryComp<MeleeWeaponComponent>(weapon, out var melee))
            return;

        // Cyclone spins faster for the same health a second.
        var whirl = 1f / (1f + Signature(weapon, FSMeleeSignature.Cyclone));
        if (WhirlwindOn(weapon))
        {
            if (CanAffordWhirl(user, WhirlHealthCost * whirl))
            {
                melee.NextAttack = Timing.CurTime + WhirlInterval * whirl;
                DirtyField(weapon, melee, nameof(MeleeWeaponComponent.NextAttack));
                OnWhirl(user, WhirlHealthCost * whirl);
                return;
            }

            OnWhirlSpent(user);
        }

        if (weapon.Comp.HeavyRecovery <= 0f)
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

    // Whirlwind keeps a spin going for as long as the heavy is held, until the wielder is too hurt to pay for it.
    private bool WhirlwindOn(Entity<FSMeleeJuiceComponent> weapon)
    {
        return weapon.Comp.HeavyStyle == FSMeleeHeavyStyle.Spin
            && TryComp<FSWeaponUpgradeStateComponent>(weapon, out var upgrades)
            && upgrades.WhirlwindEnabled;
    }

    private bool CanAffordWhirl(EntityUid user, float cost)
    {
        if (!TryComp<DamageableComponent>(user, out var damageable)
            || !TryComp<MobThresholdsComponent>(user, out var thresholds)
            || !_thresholds.TryGetThresholdForState(user, MobState.Critical, out var critical, thresholds))
            return true;

        return damageable.TotalDamage + cost < critical.Value * WhirlHealthFloor;
    }

    protected virtual void OnWhirlSpent(EntityUid user)
    {
    }

    protected virtual void OnWhirl(EntityUid user, float cost)
    {
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
        Dash(user, Vector2.Normalize(toTarget), Math.Min(distance, toTarget.Length() - 0.8f), body);
    }

    // Pushes the user about distance tiles along dir, stopping short of any wall a long dash could skip through.
    private void Dash(EntityUid user, Vector2 dir, float distance, PhysicsComponent? body = null)
    {
        if (!Resolve(user, ref body, false))
            return;

        distance = ClearDistance(user, dir, distance);
        if (distance > 0f)
            _physics.SetLinearVelocity(user, dir * distance * MobFriction, body: body);
    }

    // Carries the user along dir at a steady speed for as long as the distance takes, rather than in one shove.
    private TimeSpan SweepDash(EntityUid user, Vector2 dir, float distance)
    {
        distance = ClearDistance(user, dir, distance);
        if (distance <= 0f || !TryComp<PhysicsComponent>(user, out var body))
            return TimeSpan.Zero;

        var time = Math.Clamp(distance / SweepDashSpeed, SweepDashMin, SweepDashMax);
        var dash = EnsureComp<FSMeleeDashComponent>(user);
        dash.Velocity = dir * (distance / time);
        dash.Until = Timing.CurTime + TimeSpan.FromSeconds(time);
        Dirty(user, dash);
        _movement.RefreshFrictionModifiers(user);
        _physics.SetLinearVelocity(user, dash.Velocity, body: body);
        return TimeSpan.FromSeconds(time);
    }

    protected float ClearDistance(EntityUid user, Vector2 dir, float distance)
    {
        var xform = Transform(user);
        var ray = new CollisionRay(Xform.GetWorldPosition(xform), dir, (int) CollisionGroup.Impassable);
        foreach (var hit in _physics.IntersectRay(xform.MapID, ray, distance + WallMargin, user, returnOnFirstHit: true))
        {
            distance = Math.Min(distance, hit.Distance - WallMargin);
        }

        return distance;
    }

    private void OnDashFriction(Entity<FSMeleeDashComponent> ent, ref RefreshFrictionModifiersEvent args)
    {
        if (ent.Comp.Velocity == Vector2.Zero)
            return;

        args.ModifyFriction(0f);
        args.ModifyAcceleration(0f);
    }

    // Cuts what each dash reaches, then ends finished ones with a short skid. The client only runs its own, predicted body.
    private void UpdateDashes()
    {
        var now = Timing.CurTime;
        _dashing.Clear();
        var dashes = EntityQueryEnumerator<FSMeleeDashComponent, PhysicsComponent>();
        while (dashes.MoveNext(out var uid, out var dash, out var body))
        {
            if (dash.Velocity != Vector2.Zero && (_net.IsServer || body.Predict))
                _dashing.Add((uid, dash, body));
        }

        foreach (var (uid, dash, body) in _dashing)
        {
            SweepPass((uid, dash));
            if (now < dash.Until)
                continue;

            var speed = Math.Min(body.LinearVelocity.Length(), DashExitSpeed);
            _physics.SetLinearVelocity(uid, Vector2.Normalize(dash.Velocity) * speed, body: body);
            dash.Velocity = Vector2.Zero;
            dash.Weapon = null;
            dash.Primary = null;
            Dirty(uid, dash);
            _movement.RefreshFrictionModifiers(uid);
        }
    }

    // Shoving through zombies would fight the dash and pull the client and server apart.
    private void OnDashMobCollide(Entity<FSMeleeDashComponent> ent, ref AttemptMobCollideEvent args)
    {
        if (ent.Comp.Velocity != Vector2.Zero)
            args.Cancelled = true;
    }

    private void OnDashMobTargetCollide(Entity<FSMeleeDashComponent> ent, ref AttemptMobTargetCollideEvent args)
    {
        if (ent.Comp.Velocity != Vector2.Zero)
            args.Cancelled = true;
    }

    private void Guard(EntityUid user, TimeSpan until)
    {
        var guard = EnsureComp<FSMeleeGuardComponent>(user);
        if (guard.Until >= until)
            return;

        guard.Until = until;
        Dirty(user, guard);
    }

    private void OnGuardDamage(Entity<FSMeleeGuardComponent> ent, ref DamageModifyEvent args)
    {
        if (Timing.CurTime < ent.Comp.Until
            || ent.Comp.Charging is { } weapon && TryComp<FSMeleeChargeComponent>(weapon, out var charge) && charge.ChargeStart != null)
            args.Damage *= GuardDamage;
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

        if (duration < StaggerFlinch || HasComp<FSSpecialZombieComponent>(target))
            return;

        var staggered = EnsureComp<FSMeleeStaggeredComponent>(target);
        staggered.Until = end + StaggerGrace;
        Dirty(target, staggered);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateDashes();

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

        _expired.Clear();
        var staggers = EntityQueryEnumerator<FSMeleeStaggeredComponent>();
        while (staggers.MoveNext(out var uid, out var staggered))
        {
            if (now >= staggered.Until)
                _expired.Add(uid);
        }

        foreach (var uid in _expired)
        {
            RemComp<FSMeleeStaggeredComponent>(uid);
        }
    }
}
