using Content.Shared.Explosion.Components;
using Content.Shared.GameTicking;
using Content.Shared.Item;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Cleanup;

public sealed partial class FSGroundItemCleanupSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const double LifetimeSeconds = 5 * 60;
    private const float ScanInterval = 30f;

    private float _accumulator;
    private readonly Dictionary<EntityUid, TimeSpan> _groundedSince = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent _)
    {
        _accumulator = 0f;
        _groundedSince.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _accumulator += frameTime;
        if (_accumulator < ScanInterval)
            return;
        _accumulator = 0f;

        var now = _timing.CurTime;
        var toDelete = new List<EntityUid>();

        var query = EntityQueryEnumerator<GunComponent, ItemComponent>();
        while (query.MoveNext(out var uid, out _, out _))
            Check(uid, now, toDelete);

        var magQuery = EntityQueryEnumerator<BallisticAmmoProviderComponent, ItemComponent>();
        while (magQuery.MoveNext(out var uid, out _, out _))
            Check(uid, now, toDelete);

        var meleeQuery = EntityQueryEnumerator<MeleeWeaponComponent, ItemComponent>();
        while (meleeQuery.MoveNext(out var uid, out _, out _))
            Check(uid, now, toDelete);

        var explosiveQuery = EntityQueryEnumerator<ExplosiveComponent, ItemComponent>();
        while (explosiveQuery.MoveNext(out var uid, out _, out _))
            Check(uid, now, toDelete);

        var cartridgeQuery = EntityQueryEnumerator<CartridgeAmmoComponent, ItemComponent>();
        while (cartridgeQuery.MoveNext(out var uid, out _, out _))
            Check(uid, now, toDelete);

        foreach (var uid in toDelete)
        {
            _groundedSince.Remove(uid);
            QueueDel(uid);
        }
    }

    private void Check(EntityUid uid, TimeSpan now, List<EntityUid> toDelete)
    {
        if (_containers.IsEntityInContainer(uid))
        {
            _groundedSince.Remove(uid);
            return;
        }

        if (!_groundedSince.TryGetValue(uid, out var since))
        {
            _groundedSince[uid] = now;
            return;
        }

        if ((now - since).TotalSeconds >= LifetimeSeconds)
            toDelete.Add(uid);
    }
}
