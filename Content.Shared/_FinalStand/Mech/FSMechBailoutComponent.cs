using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Mech;

// A pilot thrown out of a wrecked mech gets a few seconds of extra speed to get clear.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(FSMechBailoutSystem))]
public sealed partial class FSMechBailoutComponent : Component
{
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan EndTime;

    [DataField]
    public float SpeedMultiplier = 1.3f;
}

public sealed partial class FSMechBailoutSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMechBailoutComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    public void Start(EntityUid pilot, TimeSpan duration)
    {
        var bailout = EnsureComp<FSMechBailoutComponent>(pilot);
        bailout.EndTime = _timing.CurTime + duration;
        Dirty(pilot, bailout);
        _speed.RefreshMovementSpeedModifiers(pilot);
    }

    private void OnRefreshSpeed(Entity<FSMechBailoutComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (_timing.CurTime < ent.Comp.EndTime)
            args.ModifySpeed(ent.Comp.SpeedMultiplier);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<FSMechBailoutComponent>();
        while (query.MoveNext(out var uid, out var bailout))
        {
            if (_timing.CurTime < bailout.EndTime)
                continue;

            RemCompDeferred<FSMechBailoutComponent>(uid);
            _speed.RefreshMovementSpeedModifiers(uid);
        }
    }
}
