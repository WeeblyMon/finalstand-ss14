using Content.Server._FinalStand.Spawners;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._FinalStand.Cleanup;

public sealed partial class FSCorpseCleanupSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;

    private const int MaxZombieCorpses = 50;

    private readonly Queue<EntityUid> _zombieCorpses = new();
    private readonly HashSet<Entity<WaveSpawnedTagComponent>> _nearby = new();

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

            DeleteNearbyCorpses(oldest);
            QueueDel(oldest);
        }
    }

    private void DeleteNearbyCorpses(EntityUid corpse)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(Transform(corpse).Coordinates, 1.5f, _nearby);
        foreach (var ent in _nearby)
        {
            if (ent.Owner == corpse)
                continue;
            if (!TryComp<MobStateComponent>(ent.Owner, out var ms) || ms.CurrentState != MobState.Dead)
                continue;
            QueueDel(ent.Owner);
        }
    }
}
