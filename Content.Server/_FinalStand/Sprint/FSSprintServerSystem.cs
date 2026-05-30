using System.Numerics;
using Content.Server.Damage.Systems;
using Content.Shared._FinalStand.Sprint;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Sprint;

public sealed class FSSprintServerSystem : EntitySystem
{
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

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
            if (sprint.IsSprinting && HasComp<MechPilotComponent>(uid))
            {
                ForceStopSprint(uid, sprint);
                continue;
            }

            if (sprint.IsSprinting)
            {
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
                    sprint.DustAccumulator += frameTime;
                    if (sprint.DustAccumulator >= DustInterval)
                    {
                        sprint.DustAccumulator = 0f;
                        SpawnDust(uid);
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
