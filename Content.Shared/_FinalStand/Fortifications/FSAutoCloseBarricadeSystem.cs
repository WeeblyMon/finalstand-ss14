using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Fortifications;

public sealed partial class FSAutoCloseBarricadeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedDoorSystem _door = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSAutoCloseBarricadeComponent, DoorStateChangedEvent>(OnStateChanged);
    }

    private void OnStateChanged(EntityUid uid, FSAutoCloseBarricadeComponent comp, DoorStateChangedEvent args)
    {
        if (_timing.ApplyingState || args.State != DoorState.Open)
            return;

        _door.SetNextStateChange(uid, comp.Delay);
    }
}
