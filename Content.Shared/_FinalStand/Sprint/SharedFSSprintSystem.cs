using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Input;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Shared._FinalStand.Sprint;

// Speed-modifier and start/stop-toggle logic lives here (Shared) rather than server-only, so the
// client predicts sprint locally off its own key input instead of waiting on a server round trip
// for RefreshMovementSpeedModifiersEvent to recompute the speed and sync back down.
public abstract partial class SharedFSSprintSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSSprintComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);

        SubscribeLocalEvent<FSSprintComponent, KnockedDownEvent>(OnKnockedDown);
        SubscribeLocalEvent<FSSprintComponent, StunnedEvent>(OnStunned);
        SubscribeLocalEvent<FSSprintComponent, BuckledEvent>(OnBuckled);

        SubscribeLocalEvent<FSSprintComponent, BeforeStaminaAlertEvent>(OnBeforeStaminaAlert);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.FSSprint,
                InputCmdHandler.FromDelegate(OnSprintKeyDown, OnSprintKeyUp, handle: false))
            .Register<SharedFSSprintSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<SharedFSSprintSystem>();
    }

    private void OnRefreshSpeed(EntityUid uid, FSSprintComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (comp.IsSprinting)
            args.ModifySpeed(comp.SpeedMultiplier, comp.SpeedMultiplier);
        else if (comp.IsExhausted)
            args.ModifySpeed(comp.EmptySlowMultiplier, comp.EmptySlowMultiplier);
    }

    private void OnSprintKeyDown(ICommonSession? session)
    {
        if (session?.AttachedEntity is { } uid)
            TryStartSprint(uid);
    }

    private void OnSprintKeyUp(ICommonSession? session)
    {
        if (session?.AttachedEntity is { } uid)
            TryStopSprint(uid);
    }

    public bool TryStartSprint(EntityUid uid, FSSprintComponent? sprint = null)
    {
        if (!Resolve(uid, ref sprint, false))
            return false;
        if (sprint.IsExhausted || sprint.IsSprinting)
            return false;
        if (!TryComp<StaminaComponent>(uid, out var stamina) || stamina.StaminaDamage >= stamina.CritThreshold)
            return false;

        sprint.IsSprinting = true;
        Dirty(uid, sprint);
        _movement.RefreshMovementSpeedModifiers(uid);
        return true;
    }

    public bool TryStopSprint(EntityUid uid, FSSprintComponent? sprint = null)
    {
        if (!Resolve(uid, ref sprint, false) || !sprint.IsSprinting)
            return false;

        sprint.IsSprinting = false;
        Dirty(uid, sprint);
        _movement.RefreshMovementSpeedModifiers(uid);
        return true;
    }

    public void ForceStopSprint(EntityUid uid, FSSprintComponent sprint)
    {
        if (!sprint.IsSprinting)
            return;

        sprint.IsSprinting = false;
        sprint.DustAccumulator = 0f;
        Dirty(uid, sprint);
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    private void OnKnockedDown(EntityUid uid, FSSprintComponent sprint, ref KnockedDownEvent args)
        => ForceStopSprint(uid, sprint);

    private void OnStunned(EntityUid uid, FSSprintComponent sprint, ref StunnedEvent args)
        => ForceStopSprint(uid, sprint);

    private void OnBuckled(EntityUid uid, FSSprintComponent sprint, ref BuckledEvent args)
        => ForceStopSprint(uid, sprint);

    // FS sprint players use their own HUD overlay instead of the vanilla stamina alert.
    private void OnBeforeStaminaAlert(EntityUid uid, FSSprintComponent sprint, ref BeforeStaminaAlertEvent args)
        => args.Cancelled = true;
}
