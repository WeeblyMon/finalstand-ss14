using Content.Server._FinalStand.Spawners;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.WaveHud;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.GameTicking.Rules;

// Picks spawners, places and configures wave enemies, and selects which prototype spawns next.
public sealed class WaveEnemySpawningSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly WaveEnemyScalingSystem _scaling = default!;

    private static readonly TimeSpan EnemyCountBroadcastInterval = TimeSpan.FromSeconds(0.25);
    private static readonly List<EntProtoId> FallbackEnemyPool = new() { "MobXeno" };

    private readonly List<EntityUid> _spawnerBuffer = new();
    private readonly HashSet<EntityUid> _spawnClearBuffer = new();

    // Picks a random non-empty subset of the spawners unlocked at the current wave number.
    public void SelectSpawners(WaveGameRuleComponent comp)
    {
        _spawnerBuffer.Clear();
        var query = EntityQueryEnumerator<WaveEnemySpawnerComponent>();
        while (query.MoveNext(out var spawnerUid, out var spawner))
        {
            if (comp.WaveNumber >= spawner.FromWave)
                _spawnerBuffer.Add(spawnerUid);
        }

        comp.SpawnerEntities.Clear();
        if (_spawnerBuffer.Count == 0)
            return;

        if (_spawnerBuffer.Count > 1)
            _random.Shuffle(_spawnerBuffer);

        var activeCount = _random.Next(1, _spawnerBuffer.Count + 1);
        for (var i = 0; i < activeCount; i++)
            comp.SpawnerEntities.Add(_spawnerBuffer[i]);
    }

    private bool IsSpawnClear(EntityCoordinates coords)
    {
        var mapCoords = _transform.ToMapCoordinates(coords);
        _spawnClearBuffer.Clear();
        _lookup.GetEntitiesInRange(mapCoords.MapId, mapCoords.Position, 0.4f, _spawnClearBuffer, LookupFlags.Static | LookupFlags.Approximate);
        foreach (var ent in _spawnClearBuffer)
        {
            if (TryComp<PhysicsComponent>(ent, out var body) && body.Hard && body.CanCollide && body.BodyType == BodyType.Static)
                return false;
        }
        return true;
    }

    public void SpawnNextBatch(EntityUid uid, WaveGameRuleComponent comp)
    {
        if (comp.SpawnerEntities.Count == 0)
        {
            SelectSpawners(comp);
            comp.NextSpawnTime = _timing.CurTime + TimeSpan.FromSeconds(1);
            if (comp.SpawnerEntities.Count == 0)
                return;
        }

        var pool = GetDirectorPool(comp);
        var remaining = comp.EnemyTotalThisWave - comp.EnemiesSpawnedThisWave;
        var toSpawn = Math.Min(remaining, comp.SpawnerEntities.Count * comp.SpawnBatchSize);

        for (var i = 0; i < toSpawn; i++)
        {
            var spawnerUid = comp.SpawnerEntities[i % comp.SpawnerEntities.Count];

            var coords = Transform(spawnerUid).Coordinates;
            if (TryComp<WaveEnemySpawnerComponent>(spawnerUid, out var spawnerComp) && spawnerComp.SpawnRadius > 0f)
            {
                // If all attempts hit walls, the spawner centre is used as the fallback.
                for (var attempt = 0; attempt < 8; attempt++)
                {
                    var angle = _random.NextFloat() * MathF.Tau;
                    var radius = MathF.Sqrt(_random.NextFloat()) * spawnerComp.SpawnRadius;
                    var candidate = coords.Offset(new System.Numerics.Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius));
                    if (IsSpawnClear(candidate))
                    {
                        coords = candidate;
                        break;
                    }
                }
            }

            var proto = SelectEnemyProto(comp, pool);
            var enemy = SpawnWaveEnemy(proto, coords, comp);
            comp.AliveEnemies.Add(enemy);
            comp.EnemiesSpawnedThisWave++;
        }

        var intervalSec = comp.MinSpawnInterval + _random.NextFloat() * (comp.MaxSpawnInterval - comp.MinSpawnInterval);
        comp.NextSpawnTime = _timing.CurTime + TimeSpan.FromSeconds(intervalSec);
        PushEnemyCount(comp);
    }

    // Shared by the boss spawn and the regular batch spawn so the two paths can't drift apart.
    public EntityUid SpawnWaveEnemy(EntProtoId proto, EntityCoordinates coords, WaveGameRuleComponent comp)
    {
        var enemy = Spawn(proto, coords);
        EnsureComp<WaveSpawnedTagComponent>(enemy);
        EnsureComp<FSEnemyDamageTrackingComponent>(enemy);
        if (TryComp<HTNComponent>(enemy, out var htn))
        {
            // FINALSTAND issue-2: 1000f gave zombies map-wide omniscient aggro through walls.
            // Now that NearbyHostilesQuery applies an LOS check, a larger radius is fine —
            // walls filter naturally so zombies see down open corridors but not into rooms.
            htn.Blackboard.SetValue("VisionRadius", 15f);
            htn.Blackboard.SetValue("AggroVisionRadius", 15f);
            htn.Blackboard.SetValue(NPCBlackboard.NavSmash, true);
            htn.Blackboard.SetValue(NPCBlackboard.NavPry, false);
            if (comp.CCCEntity.IsValid())
                htn.Blackboard.SetValue(NPCBlackboard.CurrentOrderedTarget, comp.CCCEntity);

            // FINALSTAND: Bloater has higher aggro radius to prioritise player hunting over CCC beeline
            if (TryComp<FSBoomOnDeathComponent>(enemy, out var boom))
            {
                htn.Blackboard.SetValue("AggroVisionRadius", boom.AggroVisionRadius);
                htn.Blackboard.SetValue("VisionRadius", boom.AggroVisionRadius);
            }
        }
        _scaling.ScaleEnemyHp(enemy, comp.WaveNumber);
        _scaling.ScaleEnemySpeed(enemy, comp.WaveNumber);
        _scaling.ScaleEnemyDamage(enemy, comp.WaveNumber, comp.PlayersThisWave);
        _scaling.ScaleEnemyFireRate(enemy, comp.WaveNumber);
        RaiseLocalEvent(enemy, new FSEnemyHpScaledEvent()); // FINALSTAND: armor system recalculates MaxArmor after HP scale
        return enemy;
    }

    public void PushEnemyCount(WaveGameRuleComponent comp)
    {
        var now = _timing.CurTime;
        if (now < comp.NextEnemyCountBroadcast)
            return;

        comp.NextEnemyCountBroadcast = now + EnemyCountBroadcastInterval;
        RaiseNetworkEvent(new FSEnemyCountEvent(comp.AliveEnemies.Count, comp.EnemyTotalThisWave), Filter.Broadcast());
    }

    private EntProtoId SelectEnemyProto(WaveGameRuleComponent comp, List<EntProtoId> pool)
    {
        foreach (var special in comp.SpecialEnemyPool)
        {
            if (comp.WaveNumber >= special.FromWave && _random.NextFloat() < special.SpawnChance)
                return special.EnemyId;
        }
        return _random.Pick(pool);
    }

    public static List<EntProtoId> GetDirectorPool(WaveGameRuleComponent comp)
    {
        WaveEnemyConfig? match = null;
        foreach (var config in comp.EnemyConfigs)
        {
            if (config.FromWave <= comp.WaveNumber &&
                (config.ToWave == null || comp.WaveNumber <= config.ToWave))
            {
                match = config;
            }
        }
        return match?.EnemyPool ?? FallbackEnemyPool;
    }
}
