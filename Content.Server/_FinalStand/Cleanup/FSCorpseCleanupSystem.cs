using Content.Server._FinalStand.Spawners;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;

namespace Content.Server._FinalStand.Cleanup;

public sealed class FSCorpseCleanupSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private const int MaxZombieCorpses = 50;

    private readonly Queue<EntityUid> _zombieCorpses = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    /// <summary>
    /// Called by WaveGameRuleSystem when a wave enemy transitions to Dead.
    /// </summary>
    public void TrackZombieDeath(EntityUid uid)
    {
        _zombieCorpses.Enqueue(uid);
        TrimCorpses();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent _) => _zombieCorpses.Clear();

    private void TrimCorpses()
    {
        while (_zombieCorpses.Count > MaxZombieCorpses)
        {
            var oldest = _zombieCorpses.Dequeue();
            if (!Exists(oldest)) continue;
            if (!TryComp<MobStateComponent>(oldest, out var ms) || ms.CurrentState != MobState.Dead) continue;

            DeleteNearbyWaveItems(oldest);
            QueueDel(oldest);
        }
    }

    private void DeleteNearbyWaveItems(EntityUid corpse)
    {
        var nearby = new HashSet<Entity<WaveSpawnedTagComponent>>();
        _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(Transform(corpse).Coordinates, 1.5f, nearby);
        foreach (var ent in nearby)
        {
            if (ent.Owner == corpse)
                continue;
            if (TryComp<MobStateComponent>(ent.Owner, out var ms) && ms.CurrentState != MobState.Dead)
                continue;
            QueueDel(ent.Owner);
        }
    }
}
