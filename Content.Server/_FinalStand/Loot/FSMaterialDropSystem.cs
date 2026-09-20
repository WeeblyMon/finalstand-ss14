using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.Loot;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Loot;

public sealed class FSMaterialDropSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    private const int WavesUntilCleanup = 2;

    // Ceiling on drops loose on the floor at once; banked (picked-up) ones don't count.
    private const int MaxLooseDrops = 300;

    private int _wavesEnded;
    private readonly Queue<EntityUid> _looseDropOrder = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMaterialDropComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent _)
    {
        _wavesEnded = 0;
        _looseDropOrder.Clear();
    }

    private void OnMobStateChanged(Entity<FSMaterialDropComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || ent.Comp.Materials.Count == 0)
            return;

        if (!_random.Prob(ent.Comp.DropChance))
            return;

        var drop = Spawn(_random.Pick(ent.Comp.Materials), Transform(ent).Coordinates);
        EnsureComp<FSWaveLootComponent>(drop).DroppedOnWave = _wavesEnded;

        _looseDropOrder.Enqueue(drop);
        TrimLooseDrops();
    }

    private void TrimLooseDrops()
    {
        while (_looseDropOrder.Count > MaxLooseDrops)
        {
            var oldest = _looseDropOrder.Dequeue();

            // Already picked up or gone - it's not floor litter anymore, leave it alone.
            if (!Exists(oldest) || _container.IsEntityInContainer(oldest))
                continue;

            QueueDel(oldest);
        }
    }

    private void OnWaveEnded(ref WaveEndedEvent args)
    {
        _wavesEnded++;

        var query = EntityQueryEnumerator<FSWaveLootComponent>();
        while (query.MoveNext(out var uid, out var loot))
        {
            if (_wavesEnded - loot.DroppedOnWave < WavesUntilCleanup)
                continue;

            // Anything a player has banked is theirs; only litter left on the floor is swept up.
            if (_container.IsEntityInContainer(uid))
                continue;

            QueueDel(uid);
        }
    }
}
