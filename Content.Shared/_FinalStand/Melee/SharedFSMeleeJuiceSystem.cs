using System.Numerics;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.Damage.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Melee;

public abstract partial class SharedFSMeleeJuiceSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected SharedTransformSystem Xform = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    private readonly List<EntityUid> _expired = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSMeleeJuiceComponent, MeleeHitEvent>(OnMeleeHit, after: [typeof(FSFriendlyFireSharedSystem)]);
        SubscribeLocalEvent<FSMeleeJuiceComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<FSMeleeSwingComponent, RefreshMovementSpeedModifiersEvent>(OnSwingSpeed);
        SubscribeLocalEvent<FSMeleeFlinchComponent, RefreshMovementSpeedModifiersEvent>(OnFlinchSpeed);
        SubscribeLocalEvent<FSMeleeFlinchComponent, AttackAttemptEvent>(OnFlinchAttack);
    }

    protected abstract void OnHit(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, List<EntityUid> targets, Vector2 direction, bool heavy, bool lethal, MeleeHitEvent args);

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
        var profile = ent.Comp.Get(heavy);
        StartSwing(args.User, profile);

        var targets = new List<EntityUid>();
        foreach (var hit in args.HitEntities)
        {
            if (hit != args.User)
                targets.Add(hit);
        }

        if (targets.Count == 0)
            return;

        var userPos = Xform.GetWorldPosition(args.User);
        var direction = args.Direction ?? Xform.GetWorldPosition(targets[0]) - userPos;
        if (direction == Vector2.Zero)
            direction = Vector2.UnitY;

        var damage = args.BaseDamage.GetTotal();
        var lethal = false;
        foreach (var target in targets)
        {
            if (!TryComp<DamageableComponent>(target, out var damageable)
                || !_thresholds.TryGetThresholdForState(target, MobState.Dead, out var threshold))
                continue;

            if (damageable.TotalDamage + damage >= threshold)
            {
                lethal = true;
                break;
            }
        }

        OnHit(ent, args.User, targets, direction, heavy, lethal, args);
    }

    private void StartSwing(EntityUid user, FSMeleeJuiceProfile profile)
    {
        if (profile.MoveAnticipation >= 1f && profile.MoveImpact >= 1f && profile.MoveRecovery >= 1f)
            return;

        var now = Timing.CurTime;
        var swing = EnsureComp<FSMeleeSwingComponent>(user);
        swing.AnticipationEnd = now + TimeSpan.FromSeconds(profile.Anticipation);
        swing.ImpactEnd = swing.AnticipationEnd + TimeSpan.FromSeconds(profile.Hitstop + 0.1f);
        swing.RecoveryEnd = now + TimeSpan.FromSeconds(Math.Max(profile.RecoveryTime, profile.Anticipation + profile.Hitstop + 0.1f));
        swing.MoveAnticipation = profile.MoveAnticipation;
        swing.MoveImpact = profile.MoveImpact;
        swing.MoveRecovery = profile.MoveRecovery;
        Dirty(user, swing);
    }

    private float SwingMultiplier(FSMeleeSwingComponent swing, TimeSpan now)
    {
        if (now >= swing.RecoveryEnd)
            return 1f;
        if (now < swing.AnticipationEnd)
            return swing.MoveAnticipation;
        if (now < swing.ImpactEnd)
            return swing.MoveImpact;
        return swing.MoveRecovery;
    }

    private void OnSwingSpeed(Entity<FSMeleeSwingComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.AppliedMultiplier);
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

        var now = Timing.CurTime;
        var swings = EntityQueryEnumerator<FSMeleeSwingComponent>();
        while (swings.MoveNext(out var uid, out var swing))
        {
            var multiplier = SwingMultiplier(swing, now);
            if (MathHelper.CloseTo(multiplier, swing.AppliedMultiplier))
                continue;

            swing.AppliedMultiplier = multiplier;
            _movement.RefreshMovementSpeedModifiers(uid);
        }

        if (!_net.IsServer)
            return;

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
