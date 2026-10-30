using Content.Shared.Mech.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Mech;

public sealed partial class FSMechLowPowerSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.25);
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMechLowPowerComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(Entity<FSMechLowPowerComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Active)
            args.ModifySpeed(ent.Comp.SpeedModifier);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient || _timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;
        var query = EntityQueryEnumerator<FSMechLowPowerComponent, MechComponent>();
        while (query.MoveNext(out var uid, out var low, out var mech))
        {
            var active = mech.MaxEnergy > 0 && mech.Energy <= mech.MaxEnergy * low.Fraction;
            if (active == low.Active)
                continue;

            low.Active = active;
            Dirty(uid, low);
            _speed.RefreshMovementSpeedModifiers(uid);
            if (active)
                _audio.PlayPvs(low.Alarm, uid);
        }
    }
}
