using Content.Shared._FinalStand.Deployables;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Deployables;

public sealed class FSDamageBeaconFieldVfxSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSDamageBeaconFieldVfxComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, FSDamageBeaconFieldVfxComponent comp, MapInitEvent args)
    {
        comp.SpawnedAt = _timing.CurTime;
        Dirty(uid, comp);
    }
}
