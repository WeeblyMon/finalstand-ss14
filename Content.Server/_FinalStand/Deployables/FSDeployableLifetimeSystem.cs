using Content.Shared._FinalStand.Deployables;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Deployables;

public sealed partial class FSDeployableLifetimeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSDeployableLifetimeComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, FSDeployableLifetimeComponent comp, MapInitEvent args)
    {
        comp.ExpiresAt = _timing.CurTime + comp.Lifetime;
        Dirty(uid, comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSDeployableLifetimeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.ExpiresAt)
                QueueDel(uid);
        }
    }
}
