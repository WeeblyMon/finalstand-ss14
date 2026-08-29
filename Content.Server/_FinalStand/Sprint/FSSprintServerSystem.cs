using System.Numerics;
using Content.Server.Damage.Systems;
using Content.Shared.Alert;
using Content.Shared.GameTicking;
using Content.Shared._FinalStand.Sprint;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Mech.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Sprint;

public sealed partial class FSSprintServerSystem : SharedFSSprintSystem
{
    [Dependency] private StaminaSystem _stamina = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float DustInterval = 0.13f; // seconds between dust cloud spawns
    private const float MovingVelocityThresholdSq = 0.01f;

    private EntityUid? _draining;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<FSSprintComponent, BeforeStaminaDamageEvent>(OnBeforeStaminaDamage);
    }

    private void OnBeforeStaminaDamage(Entity<FSSprintComponent> ent, ref BeforeStaminaDamageEvent args)
    {
        if (args.Cancelled || args.Value <= 0f || _draining == ent.Owner)
            return;

        var cooldown = TryComp<StaminaComponent>(ent, out var stamina) ? stamina.Cooldown : 3f;
        ent.Comp.RegenBlockedUntil = _timing.CurTime + TimeSpan.FromSeconds(cooldown);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        EnsureComp<FSSprintComponent>(ev.Mob);

        // FS sprint players use their own HUD overlay — skip vanilla alert.
        if (TryComp<StaminaComponent>(ev.Mob, out var stamina))
            _alerts.ClearAlert(ev.Mob, stamina.StaminaAlert);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSSprintComponent, StaminaComponent>();
        while (query.MoveNext(out var uid, out var sprint, out var stamina))
        {
            if (sprint.IsSprinting && IsPilotingMech(uid))
            {
                ForceStopSprint(uid, sprint);
                continue;
            }

            if (sprint.IsSprinting)
            {
                // Only drain stamina if actually moving; sprinting in place costs nothing.
                var moving = TryComp<PhysicsComponent>(uid, out var phys) && phys.LinearVelocity.LengthSquared() > MovingVelocityThresholdSq;
                var willExhaust = moving && stamina.StaminaDamage + sprint.StaminaDrainRate * frameTime >= stamina.CritThreshold;

                if (willExhaust)
                {
                    sprint.IsSprinting = false;
                    sprint.IsExhausted = true;
                    sprint.EmptySlowRemaining = sprint.EmptySlowDuration;
                    sprint.DustAccumulator = 0f;
                    Dirty(uid, sprint);
                    _movement.RefreshMovementSpeedModifiers(uid);
                    continue;
                }

                if (moving)
                {
                    _draining = uid;
                    _stamina.TakeStaminaDamage(uid, sprint.StaminaDrainRate * frameTime, stamina, visual: false, silent: true);
                    _draining = null;

                    sprint.DustAccumulator += frameTime;
                    if (sprint.DustAccumulator >= DustInterval)
                    {
                        sprint.DustAccumulator = 0f;
                        SpawnDust(uid);
                    }
                }
                else
                {
                    sprint.DustAccumulator = 0f;
                }
            }
            else
            {
                sprint.DustAccumulator = 0f;

                if (sprint.IsExhausted)
                {
                    sprint.EmptySlowRemaining -= frameTime;
                    if (sprint.EmptySlowRemaining <= 0f)
                    {
                        sprint.IsExhausted = false;
                        sprint.EmptySlowRemaining = 0f;
                        Dirty(uid, sprint);
                        _movement.RefreshMovementSpeedModifiers(uid);
                    }
                }
                else if (stamina.StaminaDamage > 0f && _timing.CurTime >= sprint.RegenBlockedUntil)
                {
                    var regen = stamina.CritThreshold * (sprint.RegenRate / 100f) * frameTime;
                    _stamina.TakeStaminaDamage(uid, -regen, stamina, visual: false, silent: true);
                }
            }
        }
    }

    private void SpawnDust(EntityUid uid)
    {
        var coords = Transform(uid).Coordinates;
        var dust = Spawn("SprintAnimation", coords);

        if (TryComp<PhysicsComponent>(uid, out var physics) && physics.LinearVelocity.LengthSquared() > MovingVelocityThresholdSq)
        {
            var vel = physics.LinearVelocity;
            _transform.SetLocalRotation(dust, new Angle(MathF.Atan2(vel.Y, vel.X)));
        }
    }

    private bool IsPilotingMech(EntityUid uid)
    {
        return _container.TryGetContainingContainer((uid, null, null), out var container)
               && TryComp<MechComponent>(container.Owner, out var mech)
               && container.ID == mech.PilotSlotId;
    }
}
