using Content.Shared.GameTicking;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;

namespace Content.Server._FinalStand.Cleanup;

public sealed partial class FSCartridgeCleanupSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;

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

        var loose = 0;
        var query = EntityQueryEnumerator<CartridgeAmmoComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Spent || _containers.IsEntityInContainer(uid))
                continue;

            loose++;
            if (loose > MaxCasings)
                QueueDel(uid);
        }
    }
}
