using Content.Shared.GameTicking;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;

namespace Content.Server._FinalStand.Cleanup;

public sealed class FSCartridgeCleanupSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    private const int MaxCasings = 200;
    private const float ScanInterval = 5f;

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent _) => _accumulator = 0f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < ScanInterval)
            return;
        _accumulator = 0f;

        var casings = new List<EntityUid>();
        var query = EntityQueryEnumerator<CartridgeAmmoComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Spent) continue;
            if (_containers.IsEntityInContainer(uid)) continue;
            casings.Add(uid);
        }

        if (casings.Count <= MaxCasings)
            return;

        var toDelete = casings.Count - MaxCasings;
        for (var i = 0; i < toDelete; i++)
            QueueDel(casings[i]);
    }
}
