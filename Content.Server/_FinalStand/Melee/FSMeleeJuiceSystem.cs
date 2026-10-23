using System.Numerics;
using Content.Server._FinalStand.Crit;
using Content.Server._FinalStand.Perks;
using Content.Server._FinalStand.Shop;
using Content.Server._FinalStand.Upgrades;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Effects;
using Content.Server.NPC.Components;
using Content.Server.Popups;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Melee;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Melee;

public sealed partial class FSMeleeJuiceSystem : SharedFSMeleeJuiceSystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FSPerkKillPipelineSystem _killPipeline = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private KnockbackUpgradeSystem _knockback = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementModStatusSystem _movementStatus = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private FSStyleSystem _style = default!;
    [Dependency] private FSStunOverrideSystem _stun = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private FSInventorySearchSystem _search = default!;
    [Dependency] private GunSystem _gun = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private CritSystem _crit = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private ColorFlashEffectSystem _color = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private const float WallSlamKnockback = 4f;
    private const float WallSlamFlinch = 0.8f;
    private const float WallSlamDamage = 8f;
    private const float StrikeRangeSlack = 0.5f;
    private const float LaunchMinSpeed = 8f;
    private const float SlamReach = 0.75f;
    private const float LaunchRadius = 0.7f;
    private const float LaunchKnockback = 0.5f;
    private const float LaunchFlinch = 0.75f;
    private const float WallBounce = 0.3f;
    private const float MaxBounceDelay = 0.25f;
    private const float SprintStamina = 12f;
    private const float ShotRange = 10f;
    private const float BroadsideSpread = 9f;
    private const int RipAndTearStacks = 5;
    private const float RipAndTearMoney = 0.5f;
    private const float SlamFireStacks = 3f;
    private const float CrashBleedScale = 0.2f;
    private const float RicochetSpeed = 0.6f;
    private const float RicochetProbe = 0.8f;
    private static readonly TimeSpan RicochetFlight = TimeSpan.FromSeconds(0.35);
    private static readonly TimeSpan CrashBleedTime = TimeSpan.FromSeconds(3);
    private const float WildfireRadius = 2f;
    private const float AftershockReach = 1.5f;
    private const float AftershockDamage = 0.5f;
    private static readonly TimeSpan AftershockDelay = TimeSpan.FromSeconds(0.4);
    private static readonly TimeSpan ShotRefresh = TimeSpan.FromSeconds(1);
    private const string WildfireBurst = "FSMeleeWildfireBurst";
    private const string AftershockBurst = "FSMeleeAftershockBurst";
    private const float ExecutionHeal = 6f;
    private static readonly ProtoId<DamageTypePrototype> WallSlamType = "Blunt";
    private static readonly SoundSpecifier WallSlamSound = new SoundCollectionSpecifier("FSMeleeBluntHit");
    private static readonly SoundSpecifier ExecutionSound = new SoundCollectionSpecifier("FSMeleeBoneBreak", AudioParams.Default.WithVolume(4f));

    private readonly record struct PendingBounce(EntityUid User, EntityUid Target, Vector2 Direction, float Speed, TimeSpan Due);

    private readonly List<PendingBounce> _bounces = new();

    private readonly record struct PendingStrike(EntityUid User, EntityUid Weapon, List<EntityUid> Targets, DamageSpecifier Damage, Vector2 Direction, bool Heavy, bool Finisher, int Strike, TimeSpan Due);

    private readonly List<PendingStrike> _strikes = new();

    private readonly record struct PendingAftershock(EntityUid User, EntityUid Weapon, MapCoordinates Center, DamageSpecifier Damage, FSMeleeJuiceProfile Profile, TimeSpan Due);

    private readonly List<PendingAftershock> _aftershocks = new();
    private TimeSpan _nextShotRefresh;
    private readonly Dictionary<(EntityUid User, EntityUid Target), (EntityUid Weapon, bool Heavy, bool Finisher)> _hitsThisTick = new();
    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();
    private readonly List<EntityUid> _launched = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<FSMeleeQuietShotComponent, GunMuzzleFlashAttemptEvent>(OnQuietShot);
    }

    protected override void OnHit(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, List<EntityUid> targets, Vector2 direction, bool heavy, bool finisher, bool lethal, FSMeleeJuiceProfile profile, MeleeHitEvent args)
    {
        var damage = args.BaseDamage + args.BonusDamage;

        foreach (var target in targets)
            _hitsThisTick[(user, target)] = (weapon, heavy, finisher);

        if (profile.Execution)
            Execute(user, weapon, targets);

        var living = CountLiving(targets);
        if (!heavy && living > 0 && TryComp<FSWeaponUpgradeStateComponent>(weapon, out var upgrades) && upgrades.StabHeal > 0f)
            _damageable.HealEvenly(user, FixedPoint2.New(-upgrades.StabHeal * living));

        Crits(user, weapon, targets, profile, damage);
        Opportunist(user, weapon, targets, damage);

        // Rip and Tear: a big rev dump feeds the wielder and pays out on what it kills.
        if (heavy && profile.Rev >= RipAndTearStacks && Signature(weapon, FSMeleeSignature.RipAndTear) is var feed and > 0f && living > 0)
        {
            _damageable.HealEvenly(user, FixedPoint2.New(-feed * living));
            foreach (var target in targets)
            {
                var bonus = EnsureComp<FSPendingKillBonusComponent>(target);
                bonus.MoneyPercent = Math.Max(bonus.MoneyPercent, RipAndTearMoney);
            }
        }

        if (weapon.Comp.IgniteOnFullCharge && profile.FullCharge)
        {
            foreach (var target in targets)
                Ignite(user, target);
        }

        if (profile.FlinchedBonus > 0f)
        {
            foreach (var target in targets)
            {
                if (HasComp<FSMeleeFlinchComponent>(target))
                    _damageable.ChangeDamage(target, damage * profile.FlinchedBonus, origin: user);
            }
        }

        SendHit(user, weapon, targets, direction, heavy, finisher, 0, lethal, false);
        if (living > 0)
            _style.Swing(user, weapon, heavy, finisher, living);
        ApplyImpact(user, weapon, targets, profile, damage);

        if (profile.Pierce > 0)
            Pierce(user, weapon, targets, Xform.GetMapCoordinates(targets[0]), direction, profile.PierceRange, heavy, finisher, profile, damage);

        if (profile.StaggerRadius > 0f)
        {
            var center = SlamPoint(weapon, user, direction, heavy) ?? Xform.GetMapCoordinates(targets[0]);
            Stagger(user, weapon, targets, profile, center, damage, heavy, finisher);
            QueueAftershock(user, weapon, profile, center, damage);
        }

        if (profile.ExtraStrikes <= 0)
            return;

        var strikeDamage = damage * profile.StrikeDamageFraction;
        for (var i = 1; i <= profile.ExtraStrikes; i++)
        {
            var due = Timing.CurTime + TimeSpan.FromSeconds(profile.StrikeInterval * i);
            _strikes.Add(new PendingStrike(user, weapon, new List<EntityUid>(targets), strikeDamage, direction, heavy, finisher, i, due));
        }
    }

    private void Execute(EntityUid user, Entity<FSMeleeJuiceComponent> weapon, List<EntityUid> targets)
    {
        foreach (var target in targets)
        {
            if (CanExecute(target, weapon))
                Kill(user, target);
        }
    }

    // Finishes the target outright, with the execution's sound, heal and style.
    private void Kill(EntityUid user, EntityUid target)
    {
        if (!TryComp<DamageableComponent>(target, out var damageable) || DeadThreshold(target) is not { } threshold || damageable.TotalDamage >= threshold)
            return;

        _killPipeline.MarkMeleeHit(user, target);
        _damageable.ChangeDamage(target, new DamageSpecifier(_proto.Index(WallSlamType), FixedPoint2.New((threshold - (float) damageable.TotalDamage) * 2f + 10f)), ignoreResistances: true, origin: user);
        _audio.PlayPvs(ExecutionSound, target, ExecutionSound.Params.WithVariation(0.06f));
        _damageable.HealEvenly(user, FixedPoint2.New(-ExecutionHeal));
        _style.Bonus(user, FSStyleSystem.ExecutionPoints);
    }

    // Each weapon's guaranteed crits, on top of the hit.
    private void Crits(EntityUid user, Entity<FSMeleeJuiceComponent> weapon, List<EntityUid> targets, FSMeleeJuiceProfile profile, DamageSpecifier damage)
    {
        DamageSpecifier? extra = null;
        foreach (var target in targets)
        {
            if (!profile.Crit && !TargetCrits(weapon, target) || !HasComp<MobStateComponent>(target))
                continue;

            extra ??= damage * (_crit.CalculateCritMultiplier(weapon) - 1f);
            _damageable.ChangeDamage(target, extra, origin: user);
            _crit.MarkPendingCrit(user, target);
        }
    }

    // Opportunist: zombies busy with someone else take extra damage.
    private void Opportunist(EntityUid user, EntityUid weapon, List<EntityUid> targets, DamageSpecifier damage)
    {
        if (Signature(weapon, FSMeleeSignature.Opportunist) is not (var bonus and > 0f))
            return;

        foreach (var target in targets)
        {
            if (TryComp<NPCMeleeCombatComponent>(target, out var combat) && combat.Target != user && Exists(combat.Target))
                _damageable.ChangeDamage(target, damage * bonus, origin: user);
        }
    }

    private void Ignite(EntityUid user, EntityUid target)
    {
        if (IsAlly(user, target) || !TryComp<FlammableComponent>(target, out var flammable))
            return;

        flammable.FireStacks += SlamFireStacks;
        _flammable.Ignite(target, user, flammable, ignitionSourceUser: user);
    }

    protected override bool Airborne(EntityUid target)
    {
        return base.Airborne(target) || HasComp<FSMeleeLaunchedComponent>(target);
    }

    protected override void OnTempest(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, List<EntityUid> ring, DamageSpecifier damage)
    {
        foreach (var target in ring)
        {
            _hitsThisTick[(user, target)] = (weapon, false, false);
            _killPipeline.MarkMeleeHit(user, target);
            _damageable.ChangeDamage(target, damage, origin: user);
        }

        _style.Bonus(user, FSStyleSystem.StrikePoints * ring.Count, ring.Count);
    }

    // The cutlass's off hand: a heavy fires the flintlock carried with it.
    protected override void OnHeavyShot(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, Vector2 aim)
    {
        if (weapon.Comp.ShotWeapon is not { } gunProto || weapon.Comp.ShotAmmo is not { } ammoProto)
            return;

        if (_search.FindFirst(user, new HashSet<string> { gunProto.Id }) is not { } found
            || !TryComp<GunComponent>(found.Uid, out var gun))
        {
            _popup.PopupEntity(Loc.GetString("fs-melee-no-shot-weapon"), user, user);
            return;
        }

        if (TryComp<FSMeleeShotStateComponent>(weapon, out var shots))
            SetMaxShots((weapon, shots), weapon.Comp, found.Uid);

        // Broadside: three balls in a spread.
        var spread = Signature(weapon, FSMeleeSignature.Broadside) > 0f ? new[] { 0f, -BroadsideSpread, BroadsideSpread } : new[] { 0f };
        var from = Transform(user).Coordinates;
        EnsureComp<FSMeleeQuietShotComponent>(found.Uid);
        foreach (var degrees in spread)
        {
            var dir = Angle.FromDegrees(degrees).RotateVec(Vector2.Normalize(aim));
            var to = Xform.ToCoordinates(Xform.GetMapCoordinates(user).Offset(dir * ShotRange));
            var cartridge = Spawn(ammoProto, Xform.GetMapCoordinates(user));
            _gun.Shoot((found.Uid, gun), cartridge, from, to, out _, user);
            if (!TerminatingOrDeleted(cartridge))
                QueueDel(cartridge);
        }

        RemComp<FSMeleeQuietShotComponent>(found.Uid);

        // The gun's own shot sound leaves out whoever fired it, expecting them to have predicted it.
        _audio.PlayEntity(gun.SoundGunshotModified, Filter.Entities(user), user, true);
    }

    private void OnQuietShot(Entity<FSMeleeQuietShotComponent> ent, ref GunMuzzleFlashAttemptEvent args)
    {
        args.Cancelled = true;
    }

    // The gun's magazine upgrades hold more shots.
    private void SetMaxShots(Entity<FSMeleeShotStateComponent> shots, FSMeleeJuiceComponent juice, EntityUid? gun)
    {
        var max = gun == null ? juice.ShotCharges : juice.ShotCharges + (CompOrNull<FSWeaponUpgradeStateComponent>(gun)?.MagazineSizeBonus ?? 0);
        if (shots.Comp.MaxCharges == max)
            return;

        shots.Comp.MaxCharges = max;
        shots.Comp.Charges = Math.Min(shots.Comp.Charges, max);
        Dirty(shots);
    }

    // Aftershock: a full-charge slam goes off again a moment later, wider and weaker.
    private void QueueAftershock(EntityUid user, Entity<FSMeleeJuiceComponent> weapon, FSMeleeJuiceProfile profile, MapCoordinates center, DamageSpecifier damage)
    {
        if (!profile.FullCharge || Signature(weapon, FSMeleeSignature.Aftershock) <= 0f)
            return;

        var shock = profile.Copy();
        shock.StaggerRadius *= AftershockReach;
        shock.StaggerDamage = AftershockDamage;
        _aftershocks.Add(new PendingAftershock(user, weapon, center, damage, shock, Timing.CurTime + AftershockDelay));
    }

    private void RunAftershock(PendingAftershock shock)
    {
        if (TerminatingOrDeleted(shock.User) || !TryComp<FSMeleeJuiceComponent>(shock.Weapon, out var juice))
            return;

        Stagger(shock.User, (shock.Weapon, juice), new List<EntityUid>(), shock.Profile, shock.Center, shock.Damage, true, false);
        Burst(shock.Center, AftershockBurst);
    }

    // Wildfire: a burning zombie the axe kills spreads its fire to those around it.
    private void Wildfire(EntityUid user, EntityUid source)
    {
        var center = Xform.GetMapCoordinates(source);
        _nearby.Clear();
        _lookup.GetEntitiesInRange(center, WildfireRadius, _nearby);
        foreach (var mob in _nearby)
        {
            if (mob.Owner != source && CanSpill(user, center, mob, WildfireRadius))
                Ignite(user, mob);
        }

        Burst(center, WildfireBurst);
    }

    private void Burst(MapCoordinates at, string effect)
    {
        RaiseNetworkEvent(new FSMeleeBurstEvent(GetNetCoordinates(Xform.ToCoordinates(at)), effect), Filter.Pvs(at));
    }

    protected override void OnWhirl(EntityUid user, float cost)
    {
        _damageable.ChangeDamage(user, new DamageSpecifier(_proto.Index(WallSlamType), cost), ignoreResistances: true, interruptsDoAfters: false);
        _color.RaiseEffect(Color.Red, [user], Filter.Pvs(user, entityManager: EntityManager));
        if (TryComp<ActorComponent>(user, out var actor))
            RaiseNetworkEvent(new FSDamageNumberEvent { Target = GetNetEntity(user), Amount = cost }, actor.PlayerSession);
    }

    protected override void OnSweepHit(EntityUid user, EntityUid target, DamageSpecifier damage, bool lastBreath)
    {
        if (lastBreath && Airborne(target) && !HasComp<FSSpecialZombieComponent>(target))
        {
            Kill(user, target);
            return;
        }

        _killPipeline.MarkMeleeHit(user, target);
        _damageable.ChangeDamage(target, damage, origin: user);
    }

    protected override void OnSprintAttack(EntityUid user)
    {
        _stamina.TakeStaminaDamage(user, SprintStamina, visual: false, silent: true);
    }

    // Cleaned between waves.
    private void OnPrepStarted(WavePrepStartedEvent ev)
    {
        _aftershocks.Clear();

        var query = EntityQueryEnumerator<FSMeleeBloodComponent>();
        while (query.MoveNext(out var uid, out var blood))
        {
            if (blood.Kills == 0)
                continue;

            blood.Kills = 0;
            Dirty(uid, blood);
        }
    }

    private void SendHit(EntityUid user, EntityUid weapon, List<EntityUid> targets, Vector2 direction, bool heavy, bool finisher, int strike, bool lethal, bool includeUser)
    {
        var filter = Filter.Pvs(user, entityManager: EntityManager);
        if (strike == 0 && !includeUser)
            filter.RemoveWhereAttachedEntity(e => e == user);

        RaiseNetworkEvent(new FSMeleeHitJuiceEvent(GetNetEntity(user), GetNetEntity(weapon), GetNetEntityList(targets), direction, heavy, strike, lethal, finisher), filter);
    }

    private void ApplyImpact(EntityUid user, EntityUid weapon, List<EntityUid> targets, FSMeleeJuiceProfile profile, DamageSpecifier damage)
    {
        var userPos = Xform.GetWorldPosition(user);
        var active = IsActive(weapon);
        foreach (var target in targets)
        {
            if (TerminatingOrDeleted(target) || !HasComp<MobStateComponent>(target))
                continue;

            var push = Xform.GetWorldPosition(target) - userPos;
            var pushed = profile.Knockback > 0f ? _knockback.Push(target, push, profile.Knockback, TimeSpan.FromSeconds(profile.KnockbackDuration)) : 0f;

            Flinch(target, profile.FlinchDuration, profile.FlinchSpeed);

            if (profile.Trip > 0f && !HasComp<FSSpecialZombieComponent>(target) && !HasComp<ActorComponent>(target))
            {
                _stun.TryForceStun(target, TimeSpan.FromSeconds(profile.Trip), true);
                Flinch(target, profile.Trip, 0.1f);
            }

            if (active && profile.HitStatus is { } status)
                _movementStatus.TryUpdateMovementSpeedModDuration(target, status, TimeSpan.FromSeconds(profile.HitStatusDuration), profile.HitStatusSpeed);

            if (profile.Bleed > 0f)
                _bloodstream.TryModifyBleedAmount(target, profile.Bleed);

            if (pushed >= WallSlamKnockback)
                TryWallSlam(user, target, push, pushed);

            if (profile.Launch > 0f && pushed >= LaunchMinSpeed)
                Launch(user, weapon, target, push, damage * profile.Launch, profile);
        }
    }

    private void Launch(EntityUid user, EntityUid weapon, EntityUid target, Vector2 push, DamageSpecifier damage, FSMeleeJuiceProfile profile)
    {
        var launched = EnsureComp<FSMeleeLaunchedComponent>(target);
        launched.User = user;
        launched.Weapon = weapon;
        launched.Damage = damage;
        launched.Direction = Vector2.Normalize(push);
        launched.Until = Timing.CurTime + TimeSpan.FromSeconds(profile.KnockbackDuration);
        launched.Hit.Clear();
    }

    private void UpdateLaunched()
    {
        var now = Timing.CurTime;
        _launched.Clear();
        var query = EntityQueryEnumerator<FSMeleeLaunchedComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _launched.Add(uid);
        }

        foreach (var uid in _launched)
        {
            if (!TryComp<FSMeleeLaunchedComponent>(uid, out var launched))
                continue;

            if (now >= launched.Until || TerminatingOrDeleted(launched.User) || !TryComp<FSMeleeJuiceComponent>(launched.Weapon, out var juice))
            {
                RemComp<FSMeleeLaunchedComponent>(uid);
                continue;
            }

            CrashInto(uid, launched, Profile((launched.Weapon, juice), true));
        }
    }

    // Mobs in the launched body's path are bowled over and take part of the hit.
    private void CrashInto(EntityUid body, FSMeleeLaunchedComponent launched, FSMeleeJuiceProfile profile)
    {
        var user = launched.User;
        var center = Xform.GetMapCoordinates(body);
        _nearby.Clear();
        _lookup.GetEntitiesInRange(center, LaunchRadius, _nearby);

        var struck = new List<EntityUid>();
        foreach (var mob in _nearby)
        {
            if (mob.Owner == body || mob.Owner == user || launched.Hit.Contains(mob) || !CanSpill(user, center, mob, LaunchRadius))
                continue;

            launched.Hit.Add(mob);
            struck.Add(mob);
        }

        if (struck.Count == 0)
            return;

        // The bat crits every body it bowls over, and Lucille opens them up.
        var crit = CompOrNull<FSMeleeJuiceComponent>(launched.Weapon)?.CritWhen == FSMeleeCritWhen.Crash;
        var damage = crit ? launched.Damage * _crit.CalculateCritMultiplier(launched.Weapon) : launched.Damage;
        var bleed = CompOrNull<FSWeaponUpgradeStateComponent>(launched.Weapon)?.BleedLevel ?? 0;
        foreach (var mob in struck)
        {
            _hitsThisTick[(user, mob)] = (launched.Weapon, true, false);
            _killPipeline.MarkMeleeHit(user, mob);
            _damageable.ChangeDamage(mob, damage, origin: user);
            if (crit)
                _crit.MarkPendingCrit(user, mob);

            if (bleed > 0)
            {
                var bleeding = EnsureComp<FSBleedingComponent>(mob);
                bleeding.DamagePerSecond = damage.GetTotal().Float() * CrashBleedScale * bleed;
                bleeding.ExpiresAt = Timing.CurTime + CrashBleedTime;
                bleeding.NextTickAt = Timing.CurTime;
                bleeding.Instigator = user;
            }

            var away = Xform.GetWorldPosition(mob) - center.Position;
            var push = launched.Direction + (away == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(away));
            _knockback.Push(mob, push, profile.Knockback * LaunchKnockback, TimeSpan.FromSeconds(profile.KnockbackDuration));
            Flinch(mob, profile.FlinchDuration * LaunchFlinch, profile.FlinchSpeed);
        }

        _audio.PlayPvs(WallSlamSound, body, AudioParams.Default.WithVariation(0.1f));
        _style.Bonus(user, FSStyleSystem.CrashPoints * struck.Count, struck.Count);
        SendHit(user, launched.Weapon, struck, launched.Direction, true, false, 0, false, true);
    }

    // The slam lands when the body reaches the wall, not when it was hit.
    private void TryWallSlam(EntityUid user, EntityUid target, Vector2 push, float knockback)
    {
        if (push == Vector2.Zero)
            return;

        var xform = Transform(target);
        var dir = Vector2.Normalize(push);
        var ray = new CollisionRay(Xform.GetWorldPosition(xform), dir, (int) CollisionGroup.Impassable);
        var length = knockback / MobFriction + 0.45f;
        foreach (var hit in _physics.IntersectRay(xform.MapID, ray, length, target, returnOnFirstHit: true))
        {
            // A push of v covers v / friction * (1 - e^(-friction * t)) tiles by time t.
            var travel = Math.Max(0f, hit.Distance - 0.45f) * MobFriction / knockback;
            var delay = travel >= 0.95f ? MaxBounceDelay : MathF.Min(MaxBounceDelay, -MathF.Log(1f - travel) / MobFriction);
            _bounces.Add(new PendingBounce(user, target, dir, knockback, Timing.CurTime + TimeSpan.FromSeconds(delay)));
            return;
        }
    }

    private void Bounce(PendingBounce bounce)
    {
        var target = bounce.Target;
        if (TerminatingOrDeleted(target) || _mobState.IsDead(target))
            return;

        // Ricochet: a launched body comes off the wall still flying, and can bowl over the same mobs again.
        if (TryComp<FSMeleeLaunchedComponent>(target, out var launched)
            && launched.Bounces == 0
            && Signature(launched.Weapon, FSMeleeSignature.Ricochet) > 0f)
        {
            var dir = Reflect(target, bounce.Direction);
            _physics.SetLinearVelocity(target, dir * bounce.Speed * RicochetSpeed);
            launched.Direction = dir;
            launched.Bounces++;
            launched.Hit.Clear();
            launched.Until = Timing.CurTime + RicochetFlight;
        }
        else if (TryComp<PhysicsComponent>(target, out var body))
        {
            _physics.SetLinearVelocity(target, -bounce.Direction * bounce.Speed * WallBounce, body: body);
        }

        Flinch(target, WallSlamFlinch, 0.1f);
        _style.Bonus(bounce.User, FSStyleSystem.WallSlamPoints);
        _damageable.ChangeDamage(target, new DamageSpecifier(_proto.Index(WallSlamType), WallSlamDamage), origin: bounce.User);
        _audio.PlayPvs(WallSlamSound, target, AudioParams.Default.WithVariation(0.1f));
        RaiseNetworkEvent(new FSMeleeWallBounceEvent(GetNetEntity(target), bounce.Direction), Filter.Pvs(target, entityManager: EntityManager));
    }

    // Mirrors dir off whichever side of the target the wall is on.
    private Vector2 Reflect(EntityUid target, Vector2 dir)
    {
        var xform = Transform(target);
        var pos = Xform.GetWorldPosition(xform);
        bool Blocked(Vector2 axis)
        {
            if (axis == Vector2.Zero)
                return false;

            var ray = new CollisionRay(pos, axis, (int) CollisionGroup.Impassable);
            foreach (var _ in _physics.IntersectRay(xform.MapID, ray, RicochetProbe, target, returnOnFirstHit: true))
            {
                return true;
            }

            return false;
        }

        var blockedX = Blocked(new Vector2(MathF.Sign(dir.X), 0f));
        var blockedY = Blocked(new Vector2(0f, MathF.Sign(dir.Y)));
        if (!blockedX && !blockedY)
            return -dir;

        return Vector2.Normalize(new Vector2(blockedX ? -dir.X : dir.X, blockedY ? -dir.Y : dir.Y));
    }

    private bool IsActive(EntityUid weapon)
    {
        return !TryComp<ItemToggleComponent>(weapon, out var toggle) || toggle.Activated;
    }

    // Hits up to the profile's pierce count of mobs along a line from the origin.
    private void Pierce(EntityUid user, EntityUid weapon, List<EntityUid> targets, MapCoordinates map, Vector2 direction, float range, bool heavy, bool finisher, FSMeleeJuiceProfile profile, DamageSpecifier damage)
    {
        var dir = Vector2.Normalize(direction);
        var pierced = new List<EntityUid>();

        for (var step = 0.5f; step <= range && pierced.Count < profile.Pierce; step += 0.5f)
        {
            _nearby.Clear();
            _lookup.GetEntitiesInRange(map.Offset(dir * step), 0.4f, _nearby);
            foreach (var mob in _nearby)
            {
                if (mob.Owner == user || targets.Contains(mob) || pierced.Contains(mob) || !CanSpill(user, map, mob, range))
                    continue;

                pierced.Add(mob);
                if (pierced.Count >= profile.Pierce)
                    break;
            }
        }

        if (pierced.Count == 0)
            return;

        foreach (var target in pierced)
        {
            _hitsThisTick[(user, target)] = (weapon, heavy, finisher);
            _killPipeline.MarkMeleeHit(user, target);
            _damageable.ChangeDamage(target, damage, origin: user);
        }

        SendHit(user, weapon, pierced, direction, heavy, finisher, 0, false, true);
        _style.Bonus(user, FSStyleSystem.PiercePoints * pierced.Count, pierced.Count);
        ApplyImpact(user, weapon, pierced, profile, damage);
    }

    // A slam staggers around where it lands and a dash cuts through what it passes, even with nothing in reach of the swing.
    protected override void OnHeavyMiss(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, Vector2 aim, FSMeleeJuiceProfile profile, DamageSpecifier damage)
    {
        if (profile.StaggerRadius > 0f && SlamPoint(weapon, user, aim, true) is { } point)
        {
            Stagger(user, weapon, new List<EntityUid>(), profile, point, damage * profile.DamageMultiplier, true, false);
            QueueAftershock(user, weapon, profile, point, damage * profile.DamageMultiplier);
        }

        if (profile.Dash > 0f && profile.Pierce > 0 && IsActive(weapon) && TryComp<MeleeWeaponComponent>(weapon, out var melee))
            Pierce(user, weapon, new List<EntityUid>(), Xform.GetMapCoordinates(user), aim, profile.Dash + melee.Range, true, false, profile, damage * profile.DamageMultiplier);
    }

    private MapCoordinates? SlamPoint(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, Vector2 direction, bool heavy)
    {
        if (!heavy || weapon.Comp.HeavyStyle != FSMeleeHeavyStyle.Slam || direction == Vector2.Zero || !TryComp<MeleeWeaponComponent>(weapon, out var melee))
            return null;

        return Xform.GetMapCoordinates(user).Offset(Vector2.Normalize(direction) * melee.Range * SlamReach);
    }

    private void Stagger(EntityUid user, Entity<FSMeleeJuiceComponent> weapon, List<EntityUid> targets, FSMeleeJuiceProfile profile, MapCoordinates center, DamageSpecifier damage, bool heavy, bool finisher)
    {
        DamageSpecifier? shockwave = profile.StaggerDamage > 0f ? damage * profile.StaggerDamage : null;
        _nearby.Clear();
        _lookup.GetEntitiesInRange(center, profile.StaggerRadius, _nearby);
        var staggered = 0;
        foreach (var mob in _nearby)
        {
            if (mob.Owner == user || targets.Contains(mob) || !CanSpill(user, center, mob, profile.StaggerRadius))
                continue;

            var push = Xform.GetWorldPosition(mob) - center.Position;
            _knockback.Push(mob, push, profile.Knockback * 0.6f, TimeSpan.FromSeconds(profile.KnockbackDuration));
            Flinch(mob, profile.FlinchDuration, profile.FlinchSpeed);
            staggered++;
            if (weapon.Comp.IgniteOnFullCharge && profile.FullCharge)
                Ignite(user, mob);

            if (shockwave == null)
                continue;

            _hitsThisTick[(user, mob)] = (weapon, heavy, finisher);
            _killPipeline.MarkMeleeHit(user, mob);
            _damageable.ChangeDamage(mob.Owner, shockwave, origin: user);
        }

        _style.Bonus(user, FSStyleSystem.StaggerPoints * Math.Min(staggered, 6));
    }

    // Pierces, staggers and launched bodies reach past the struck target, but not into teammates, corpses or behind walls.
    private bool CanSpill(EntityUid user, MapCoordinates from, Entity<MobStateComponent> mob, float range)
    {
        return mob.Comp.CurrentState != MobState.Dead
            && !HasComp<ActorComponent>(mob)
            && !IsAlly(user, mob)
            && _interaction.InRangeUnobstructed(from, mob.Owner, range + 0.5f);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead || args.Origin is not { } user)
            return;

        if (!_hitsThisTick.TryGetValue((user, args.Target), out var hit)
            || !TryComp<FSMeleeJuiceComponent>(hit.Weapon, out var juice))
            return;

        var profile = Profile((hit.Weapon, juice), hit.Heavy, hit.Finisher);
        _style.Kill(user, hit.Heavy, hit.Finisher);
        if (TryComp<FSMeleeBloodComponent>(hit.Weapon, out var blood) && HasComp<BloodstreamComponent>(args.Target))
        {
            blood.Kills++;
            Dirty(hit.Weapon, blood);
        }

        // Quick Reload: melee kills bring the next shot back sooner.
        if (Signature(hit.Weapon, FSMeleeSignature.QuickReload) is var reload and > 0f && TryComp<FSMeleeShotStateComponent>(hit.Weapon, out var shots) && shots.Charges < shots.MaxCharges)
        {
            shots.NextCharge -= TimeSpan.FromSeconds(reload);
            Dirty(hit.Weapon, shots);
        }

        if (Signature(hit.Weapon, FSMeleeSignature.Wildfire) > 0f && TryComp<FlammableComponent>(args.Target, out var burning) && burning.OnFire)
            Wildfire(user, args.Target);

        // Bloodthirsty: a kill off a rev dump makes the next grind hit crit.
        if (hit.Heavy && Signature(hit.Weapon, FSMeleeSignature.Bloodthirsty) > 0f && TryComp<FSMeleeRevComponent>(hit.Weapon, out var rev) && !rev.CritReady)
        {
            rev.CritReady = true;
            Dirty(hit.Weapon, rev);
        }

        _audio.PlayPvs(profile.KillSound, args.Target, profile.KillSound?.Params.WithVariation(0.08f));
        RaiseNetworkEvent(new FSMeleeKillJuiceEvent(GetNetEntity(user), GetNetEntity(hit.Weapon), GetNetEntity(args.Target), hit.Heavy, hit.Finisher),
            Filter.Pvs(args.Target, entityManager: EntityManager));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _hitsThisTick.Clear();

        var now = Timing.CurTime;
        for (var i = _strikes.Count - 1; i >= 0; i--)
        {
            var strike = _strikes[i];
            if (now < strike.Due)
                continue;

            _strikes.RemoveAt(i);
            RunStrike(strike);
        }

        for (var i = _aftershocks.Count - 1; i >= 0; i--)
        {
            var shock = _aftershocks[i];
            if (now < shock.Due)
                continue;

            _aftershocks.RemoveAt(i);
            RunAftershock(shock);
        }

        for (var i = _bounces.Count - 1; i >= 0; i--)
        {
            var bounce = _bounces[i];
            if (now < bounce.Due)
                continue;

            _bounces.RemoveAt(i);
            Bounce(bounce);
        }

        UpdateLaunched();
        UpdateShots(now);
    }

    private void UpdateShots(TimeSpan now)
    {
        var refresh = now >= _nextShotRefresh;
        if (refresh)
            _nextShotRefresh = now + ShotRefresh;

        var query = EntityQueryEnumerator<FSMeleeShotStateComponent, FSMeleeJuiceComponent>();
        while (query.MoveNext(out var uid, out var shots, out var juice))
        {
            if (refresh && juice.ShotWeapon is { } gunProto
                && _container.TryGetContainingContainer(uid, out var hand) && HasComp<HandsComponent>(hand.Owner))
                SetMaxShots((uid, shots), juice, _search.FindFirst(hand.Owner, new HashSet<string> { gunProto.Id })?.Uid);

            if (shots.Charges >= shots.MaxCharges || now < shots.NextCharge)
                continue;

            shots.Charges++;
            shots.NextCharge = now + TimeSpan.FromSeconds(juice.ShotRecharge);
            Dirty(uid, shots);
        }
    }

    private void RunStrike(PendingStrike strike)
    {
        if (TerminatingOrDeleted(strike.User)
            || _mobState.IsIncapacitated(strike.User)
            || !TryComp<FSMeleeJuiceComponent>(strike.Weapon, out var juice)
            || !Melee.TryGetWeapon(strike.User, out var held, out var melee)
            || held != strike.Weapon)
            return;

        var userPos = Xform.GetWorldPosition(strike.User);
        var reach = melee.Range + StrikeRangeSlack;
        strike.Targets.RemoveAll(t => TerminatingOrDeleted(t) || (Xform.GetWorldPosition(t) - userPos).Length() > reach);
        if (strike.Targets.Count == 0)
            return;

        foreach (var target in strike.Targets)
        {
            _hitsThisTick[(strike.User, target)] = (strike.Weapon, strike.Heavy, strike.Finisher);
            _killPipeline.MarkMeleeHit(strike.User, target);
            _damageable.ChangeDamage(target, strike.Damage, origin: strike.User);
        }

        var profile = Profile((strike.Weapon, juice), strike.Heavy, strike.Finisher);
        _audio.PlayPvs(profile.StrikeSound, strike.Targets[0], profile.StrikeSound?.Params.WithPitchScale(1f + profile.StrikePitchStep * strike.Strike));
        SendHit(strike.User, strike.Weapon, strike.Targets, strike.Direction, strike.Heavy, strike.Finisher, strike.Strike, false, false);
        var living = CountLiving(strike.Targets);
        _style.Bonus(strike.User, FSStyleSystem.StrikePoints * living, living);
        ApplyImpact(strike.User, strike.Weapon, strike.Targets, profile, strike.Damage);
    }
}
