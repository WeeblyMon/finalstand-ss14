using System.Numerics;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.Melee;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Physics;
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
    [Dependency] private KnockbackUpgradeSystem _knockback = default!;
    [Dependency] private MovementModStatusSystem _movementStatus = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private const float WallSlamKnockback = 4f;
    private const float WallSlamFlinch = 0.8f;
    private const float WallSlamDamage = 8f;
    private static readonly ProtoId<DamageTypePrototype> WallSlamType = "Blunt";
    private static readonly SoundSpecifier WallSlamSound = new SoundCollectionSpecifier("FSMeleeBluntHit");

    private readonly record struct PendingStrike(EntityUid User, EntityUid Weapon, List<EntityUid> Targets, DamageSpecifier Damage, Vector2 Direction, bool Heavy, bool Finisher, int Strike, TimeSpan Due);

    private readonly List<PendingStrike> _strikes = new();
    private readonly Dictionary<(EntityUid User, EntityUid Target), (EntityUid Weapon, bool Heavy, bool Finisher)> _hitsThisTick = new();
    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    protected override void OnHit(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, List<EntityUid> targets, Vector2 direction, bool heavy, bool finisher, bool lethal, MeleeHitEvent args)
    {
        var profile = weapon.Comp.Get(heavy, finisher);
        var damage = args.BaseDamage + args.BonusDamage;

        if (profile.FlinchedBonus > 0f)
        {
            foreach (var target in targets)
            {
                if (HasComp<FSMeleeFlinchComponent>(target))
                    _damageable.ChangeDamage(target, damage * profile.FlinchedBonus, origin: user);
            }
        }

        SendHit(user, weapon, targets, direction, heavy, finisher, 0, lethal, false);
        ApplyImpact(user, weapon, targets, profile);

        foreach (var target in targets)
            _hitsThisTick[(user, target)] = (weapon, heavy, finisher);

        if (profile.Pierce > 0)
            Pierce(user, weapon, targets, direction, heavy, finisher, profile, damage);

        if (profile.StaggerRadius > 0f)
            Stagger(user, targets, profile);

        if (profile.ExtraStrikes <= 0)
            return;

        var strikeDamage = damage * profile.StrikeDamageFraction;
        for (var i = 1; i <= profile.ExtraStrikes; i++)
        {
            var due = Timing.CurTime + TimeSpan.FromSeconds(profile.StrikeInterval * i);
            _strikes.Add(new PendingStrike(user, weapon, new List<EntityUid>(targets), strikeDamage, direction, heavy, finisher, i, due));
        }
    }

    private void SendHit(EntityUid user, EntityUid weapon, List<EntityUid> targets, Vector2 direction, bool heavy, bool finisher, int strike, bool lethal, bool includeUser)
    {
        var filter = Filter.Pvs(user, entityManager: EntityManager);
        if (strike == 0 && !includeUser)
            filter.RemoveWhereAttachedEntity(e => e == user);

        RaiseNetworkEvent(new FSMeleeHitJuiceEvent(GetNetEntity(user), GetNetEntity(weapon), GetNetEntityList(targets), direction, heavy, strike, lethal, finisher), filter);
    }

    private void ApplyImpact(EntityUid user, EntityUid weapon, List<EntityUid> targets, FSMeleeJuiceProfile profile)
    {
        var userPos = Xform.GetWorldPosition(user);
        var active = !TryComp<ItemToggleComponent>(weapon, out var toggle) || toggle.Activated;
        foreach (var target in targets)
        {
            if (TerminatingOrDeleted(target) || !HasComp<MobStateComponent>(target))
                continue;

            var push = Xform.GetWorldPosition(target) - userPos;
            if (profile.Knockback > 0f)
                _knockback.Push(target, push, profile.Knockback, TimeSpan.FromSeconds(profile.KnockbackDuration));

            Flinch(target, profile.FlinchDuration, profile.FlinchSpeed);

            if (active && profile.HitStatus is { } status)
                _movementStatus.TryUpdateMovementSpeedModDuration(target, status, TimeSpan.FromSeconds(profile.HitStatusDuration), profile.HitStatusSpeed);

            if (profile.Bleed > 0f)
                _bloodstream.TryModifyBleedAmount(target, profile.Bleed);

            if (profile.Knockback >= WallSlamKnockback)
                TryWallSlam(user, target, push, profile.Knockback);
        }
    }

    private void TryWallSlam(EntityUid user, EntityUid target, Vector2 push, float knockback)
    {
        if (push == Vector2.Zero)
            return;

        var xform = Transform(target);
        var ray = new CollisionRay(Xform.GetWorldPosition(xform), Vector2.Normalize(push), (int) CollisionGroup.Impassable);
        var length = knockback / MobFriction + 0.45f;
        foreach (var _ in _physics.IntersectRay(xform.MapID, ray, length, target, returnOnFirstHit: true))
        {
            Flinch(target, WallSlamFlinch, 0.1f);
            _damageable.ChangeDamage(target, new DamageSpecifier(_proto.Index(WallSlamType), WallSlamDamage), origin: user);
            _audio.PlayPvs(WallSlamSound, target, AudioParams.Default.WithVariation(0.1f));
            return;
        }
    }

    private void Pierce(EntityUid user, EntityUid weapon, List<EntityUid> targets, Vector2 direction, bool heavy, bool finisher, FSMeleeJuiceProfile profile, DamageSpecifier damage)
    {
        var map = Xform.GetMapCoordinates(targets[0]);
        var dir = Vector2.Normalize(direction);
        var pierced = new List<EntityUid>();

        for (var step = 0.5f; step <= profile.PierceRange && pierced.Count < profile.Pierce; step += 0.5f)
        {
            _nearby.Clear();
            _lookup.GetEntitiesInRange(map.Offset(dir * step), 0.4f, _nearby);
            foreach (var mob in _nearby)
            {
                if (mob.Owner == user || targets.Contains(mob) || pierced.Contains(mob) || HasComp<ActorComponent>(mob))
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
            _damageable.ChangeDamage(target, damage, origin: user);
        }

        SendHit(user, weapon, pierced, direction, heavy, finisher, 0, false, true);
        ApplyImpact(user, weapon, pierced, profile);
    }

    private void Stagger(EntityUid user, List<EntityUid> targets, FSMeleeJuiceProfile profile)
    {
        var center = Xform.GetMapCoordinates(targets[0]);
        _nearby.Clear();
        _lookup.GetEntitiesInRange(center, profile.StaggerRadius, _nearby);
        foreach (var mob in _nearby)
        {
            if (mob.Owner == user || targets.Contains(mob) || HasComp<ActorComponent>(mob))
                continue;

            var push = Xform.GetWorldPosition(mob) - center.Position;
            _knockback.Push(mob, push, profile.Knockback * 0.6f, TimeSpan.FromSeconds(profile.KnockbackDuration));
            Flinch(mob, profile.FlinchDuration, profile.FlinchSpeed);
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead || args.Origin is not { } user)
            return;

        if (!_hitsThisTick.TryGetValue((user, args.Target), out var hit)
            || !TryComp<FSMeleeJuiceComponent>(hit.Weapon, out var juice))
            return;

        var profile = juice.Get(hit.Heavy, hit.Finisher);
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
    }

    private void RunStrike(PendingStrike strike)
    {
        if (TerminatingOrDeleted(strike.User) || !TryComp<FSMeleeJuiceComponent>(strike.Weapon, out var juice))
            return;

        strike.Targets.RemoveAll(t => TerminatingOrDeleted(t));
        if (strike.Targets.Count == 0)
            return;

        foreach (var target in strike.Targets)
        {
            _hitsThisTick[(strike.User, target)] = (strike.Weapon, strike.Heavy, strike.Finisher);
            _damageable.ChangeDamage(target, strike.Damage, origin: strike.User);
        }

        var profile = juice.Get(strike.Heavy, strike.Finisher);
        _audio.PlayPvs(profile.StrikeSound, strike.Targets[0], profile.StrikeSound?.Params.WithPitchScale(1f + profile.StrikePitchStep * strike.Strike));
        SendHit(strike.User, strike.Weapon, strike.Targets, strike.Direction, strike.Heavy, strike.Finisher, strike.Strike, false, false);
        ApplyImpact(strike.User, strike.Weapon, strike.Targets, profile);
    }
}
