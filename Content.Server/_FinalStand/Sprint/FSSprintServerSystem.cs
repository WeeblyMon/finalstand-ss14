using System.Numerics;
using Content.Server.Damage.Systems;
using Content.Shared.Alert;
using Content.Shared.GameTicking;
using Content.Shared._FinalStand.Sprint;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Sprint;

public sealed class FSSprintServerSystem : EntitySystem
{
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private const float DustInterval = 0.13f; // seconds between dust cloud spawns

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSSprintComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);

        SubscribeNetworkEvent<FSSprintStartMessage>(OnSprintStart);
        SubscribeNetworkEvent<FSSprintStopMessage>(OnSprintStop);

        SubscribeLocalEvent<FSSprintComponent, KnockedDownEvent>(OnKnockedDown);
        SubscribeLocalEvent<FSSprintComponent, StunnedEvent>(OnStunned);
        SubscribeLocalEvent<FSSprintComponent, BuckledEvent>(OnBuckled);

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (HasComp<FSSprintComponent>(ev.Mob))
        {
            Log.Debug($"[FSSprint] Player {ev.Player?.Name} spawned as {ToPrettyString(ev.Mob)} — FSSprintComponent present.");
        }
        else
        {
            Log.Warning($"[FSSprint] Player {ev.Player?.Name} spawned as {ToPrettyString(ev.Mob)} WITHOUT FSSprintComponent — adding now.");
            EnsureComp<FSSprintComponent>(ev.Mob);
        }

        // Clear the vanilla stamina alert — FS sprint players use the custom HUD overlay instead.
        if (TryComp<StaminaComponent>(ev.Mob, out var stamina))
            _alerts.ClearAlert(ev.Mob, stamina.StaminaAlert);
    }

    private void OnRefreshSpeed(EntityUid uid, FSSprintComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (comp.IsSprinting)
            args.ModifySpeed(comp.SpeedMultiplier, comp.SpeedMultiplier);
        else if (comp.IsExhausted)
            args.ModifySpeed(comp.EmptySlowMultiplier, comp.EmptySlowMultiplier);
    }

    private void OnSprintStart(FSSprintStartMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;
        if (!TryComp<FSSprintComponent>(uid, out var sprint))
            return;
        if (sprint.IsExhausted || sprint.IsSprinting)
            return;
        if (!TryComp<StaminaComponent>(uid, out var stamina))
            return;
        if (stamina.StaminaDamage >= stamina.CritThreshold)
            return;

        sprint.IsSprinting = true;
        Dirty(uid, sprint);
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    private void OnSprintStop(FSSprintStopMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;
        if (!TryComp<FSSprintComponent>(uid, out var sprint))
            return;
        if (!sprint.IsSprinting)
            return;

        sprint.IsSprinting = false;
        Dirty(uid, sprint);
        _movement.RefreshMovementSpeedModifiers(uid);
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
                var moving = TryComp<PhysicsComponent>(uid, out var phys) && phys.LinearVelocity.LengthSquared() > 0.01f;
                if (moving)
                    _stamina.TakeStaminaDamage(uid, sprint.StaminaDrainRate * frameTime, stamina, visual: false);

                if (stamina.StaminaDamage >= stamina.CritThreshold)
                {
                    sprint.IsSprinting = false;
                    sprint.IsExhausted = true;
                    sprint.EmptySlowRemaining = sprint.EmptySlowDuration;
                    sprint.DustAccumulator = 0f;
                    Dirty(uid, sprint);
                    _movement.RefreshMovementSpeedModifiers(uid);
                }
                else
                {
                    if (moving)
                    {
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
                else if (stamina.StaminaDamage > 0f)
                {
                    var regen = stamina.CritThreshold * (sprint.RegenRate / 100f) * frameTime;
                    _stamina.TakeStaminaDamage(uid, -regen, stamina, visual: false);
                }
            }
        }
    }

    private void SpawnDust(EntityUid uid)
    {
        var coords = Transform(uid).Coordinates;
        var dust = Spawn("SprintAnimation", coords);

        if (TryComp<PhysicsComponent>(uid, out var physics) && physics.LinearVelocity.LengthSquared() > 0.01f)
        {
            var vel = physics.LinearVelocity;
            _transform.SetLocalRotation(dust, new Angle(MathF.Atan2(vel.Y, vel.X)));
        }
    }

    private void OnKnockedDown(EntityUid uid, FSSprintComponent sprint, ref KnockedDownEvent args)
        => ForceStopSprint(uid, sprint);

    private void OnStunned(EntityUid uid, FSSprintComponent sprint, ref StunnedEvent args)
        => ForceStopSprint(uid, sprint);

    private void OnBuckled(EntityUid uid, FSSprintComponent sprint, ref BuckledEvent args)
        => ForceStopSprint(uid, sprint);

    private bool IsPilotingMech(EntityUid uid)
    {
        return _container.TryGetContainingContainer((uid, null, null), out var container)
               && TryComp<MechComponent>(container.Owner, out var mech)
               && container.ID == mech.PilotSlotId;
    }

    private void ForceStopSprint(EntityUid uid, FSSprintComponent sprint)
    {
        if (!sprint.IsSprinting)
            return;
        sprint.IsSprinting = false;
        sprint.DustAccumulator = 0f;
        Dirty(uid, sprint);
        _movement.RefreshMovementSpeedModifiers(uid);
    }
}
