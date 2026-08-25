using Content.Shared.Movement.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Mobs;

public sealed partial class FSRevenantBoundSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    private readonly List<EntityUid> _expired = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRevenantBoundComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<FSRevenantBoundComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSRevenantBoundComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnRefreshSpeed(EntityUid uid, FSRevenantBoundComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (_timing.CurTime >= comp.ExpiresAt)
            return;

        args.ModifySpeed(0f, 0f);
    }

    private void OnStartup(EntityUid uid, FSRevenantBoundComponent comp, ComponentStartup args)
    {
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    private void OnShutdown(EntityUid uid, FSRevenantBoundComponent comp, ComponentShutdown args)
    {
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _expired.Clear();

        var query = EntityQueryEnumerator<FSRevenantBoundComponent>();
        while (query.MoveNext(out var uid, out var bound))
        {
            if (now < bound.ExpiresAt || bound.Released)
                continue;

            bound.Released = true;
            _movement.RefreshMovementSpeedModifiers(uid);
            _expired.Add(uid);
        }

        if (!_net.IsServer)
            return;

        foreach (var uid in _expired)
            RemComp<FSRevenantBoundComponent>(uid);
    }
}
