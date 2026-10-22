using System.Numerics;
using Content.Server._FinalStand.Perks;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Melee;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Shared.Audio;
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
    private const float ExecutionHeal = 6f;
    private static readonly ProtoId<DamageTypePrototype> WallSlamType = "Blunt";
    private static readonly SoundSpecifier WallSlamSound = new SoundCollectionSpecifier("FSMeleeBluntHit");
    private static readonly SoundSpecifier ExecutionSound = new SoundCollectionSpecifier("FSMeleeBoneBreak", AudioParams.Default.WithVolume(4f));

    private readonly record struct PendingBounce(EntityUid User, EntityUid Target, Vector2 Direction, float Speed, TimeSpan Due);

    private readonly List<PendingBounce> _bounces = new();

    private readonly record struct PendingStrike(EntityUid User, EntityUid Weapon, List<EntityUid> Targets, DamageSpecifier Damage, Vector2 Direction, bool Heavy, bool Finisher, int Strike, TimeSpan Due);

    private readonly List<PendingStrike> _strikes = new();
    private readonly Dictionary<(EntityUid User, EntityUid Target), (EntityUid Weapon, bool Heavy, bool Finisher)> _hitsThisTick = new();
    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();
    private readonly List<EntityUid> _launched = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    protected override void OnHit(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, List<EntityUid> targets, Vector2 direction, bool heavy, bool finisher, bool lethal, FSMeleeJuiceProfile profile, MeleeHitEvent args)
    {
        var damage = args.BaseDamage + args.BonusDamage;

        foreach (var target in targets)
            _hitsThisTick[(user, target)] = (weapon, heavy, finisher);

        if (profile.Execution)
            Execute(user, targets);

        if (profile.FlinchedBonus > 0f)
        {
            foreach (var target in targets)
            {
                if (HasComp<FSMeleeFlinchComponent>(target))
                    _damageable.ChangeDamage(target, damage * profile.FlinchedBonus, origin: user);
            }
        }

        SendHit(user, weapon, targets, direction, heavy, finisher, 0, lethal, false);
        _style.Swing(user, weapon, heavy, finisher, targets.Count);
        ApplyImpact(user, weapon, targets, profile, damage);

        if (profile.Pierce > 0)
            Pierce(user, weapon, targets, Xform.GetMapCoordinates(targets[0]), direction, profile.PierceRange, heavy, finisher, profile, damage);

        if (profile.StaggerRadius > 0f)
            Stagger(user, targets, profile, SlamPoint(weapon, user, direction, heavy) ?? Xform.GetMapCoordinates(targets[0]));

        if (profile.ExtraStrikes <= 0)
            return;

        var strikeDamage = damage * profile.StrikeDamageFraction;
        for (var i = 1; i <= profile.ExtraStrikes; i++)
        {
            var due = Timing.CurTime + TimeSpan.FromSeconds(profile.StrikeInterval * i);
            _strikes.Add(new PendingStrike(user, weapon, new List<EntityUid>(targets), strikeDamage, direction, heavy, finisher, i, due));
        }
    }

    private void Execute(EntityUid user, List<EntityUid> targets)
    {
        var executed = 0;
        foreach (var target in targets)
        {
            if (!CanExecute(target) || !TryComp<DamageableComponent>(target, out var damageable) || HealthLost(target) is not { } lost)
                continue;

            var remaining = (float) damageable.TotalDamage / lost - (float) damageable.TotalDamage;
            _killPipeline.MarkMeleeHit(user, target);
            _damageable.ChangeDamage(target, new DamageSpecifier(_proto.Index(WallSlamType), FixedPoint2.New(remaining * 2f + 10f)), ignoreResistances: true, origin: user);
            _audio.PlayPvs(ExecutionSound, target, ExecutionSound.Params.WithVariation(0.06f));
            executed++;
        }

        if (executed == 0)
            return;

        _damageable.HealEvenly(user, FixedPoint2.New(-ExecutionHeal * executed));
        _style.Bonus(user, FSStyleSystem.ExecutionPoints * executed);
    }

    protected override void OnSprintAttack(EntityUid user)
    {
        _stamina.TakeStaminaDamage(user, SprintStamina, visual: false, silent: true);
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

        foreach (var mob in struck)
        {
            _hitsThisTick[(user, mob)] = (launched.Weapon, true, false);
            _killPipeline.MarkMeleeHit(user, mob);
            _damageable.ChangeDamage(mob, launched.Damage, origin: user);

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

        if (TryComp<PhysicsComponent>(target, out var body))
            _physics.SetLinearVelocity(target, -bounce.Direction * bounce.Speed * WallBounce, body: body);

        Flinch(target, WallSlamFlinch, 0.1f);
        _style.Bonus(bounce.User, FSStyleSystem.WallSlamPoints);
        _damageable.ChangeDamage(target, new DamageSpecifier(_proto.Index(WallSlamType), WallSlamDamage), origin: bounce.User);
        _audio.PlayPvs(WallSlamSound, target, AudioParams.Default.WithVariation(0.1f));
        RaiseNetworkEvent(new FSMeleeWallBounceEvent(GetNetEntity(target), bounce.Direction), Filter.Pvs(target, entityManager: EntityManager));
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
            Stagger(user, new List<EntityUid>(), profile, point);

        if (profile.Dash > 0f && profile.Pierce > 0 && IsActive(weapon) && TryComp<MeleeWeaponComponent>(weapon, out var melee))
            Pierce(user, weapon, new List<EntityUid>(), Xform.GetMapCoordinates(user), aim, profile.Dash + melee.Range, true, false, profile, damage * profile.DamageMultiplier);
    }

    private MapCoordinates? SlamPoint(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, Vector2 direction, bool heavy)
    {
        if (!heavy || weapon.Comp.HeavyStyle != FSMeleeHeavyStyle.Slam || direction == Vector2.Zero || !TryComp<MeleeWeaponComponent>(weapon, out var melee))
            return null;

        return Xform.GetMapCoordinates(user).Offset(Vector2.Normalize(direction) * melee.Range * SlamReach);
    }

    private void Stagger(EntityUid user, List<EntityUid> targets, FSMeleeJuiceProfile profile, MapCoordinates center)
    {
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

        for (var i = _bounces.Count - 1; i >= 0; i--)
        {
            var bounce = _bounces[i];
            if (now < bounce.Due)
                continue;

            _bounces.RemoveAt(i);
            Bounce(bounce);
        }

        UpdateLaunched();
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
        _style.Bonus(strike.User, FSStyleSystem.StrikePoints * strike.Targets.Count, strike.Targets.Count);
        ApplyImpact(strike.User, strike.Weapon, strike.Targets, profile, strike.Damage);
    }
}
