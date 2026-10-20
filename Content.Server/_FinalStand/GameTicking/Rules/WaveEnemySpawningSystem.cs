using System.Linq;
using Content.Server._FinalStand.CCC;
using Content.Shared._FinalStand.CCC;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.GameTicking.Rules;

public sealed partial class WaveEnemySpawningSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private WaveEnemyScalingSystem _scaling = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;

    private static readonly IReadOnlyList<EntProtoId> FallbackEnemyPool = new List<EntProtoId> { "MobXeno" };
    private static readonly EntProtoId RevenantProto = "FSZombieRevenant";

    private const int MaxAliveRevenants = 3;

    private readonly List<EntityUid> _spawnerBuffer = new();
    private readonly List<EntityUid> _secondaryBuffer = new();
    private readonly List<EntityUid> _spawnPlan = new();
    private readonly HashSet<EntityUid> _spawnClearBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveEnemySpawnerComponent, ComponentShutdown>(OnSpawnerShutdown);
    }

    // Spawners are picked at prep start; one deleted after that (admin, map edit) leaves the lists, and an
    // emptied list picks again on the next batch.
    private void OnSpawnerShutdown(Entity<WaveEnemySpawnerComponent> ent, ref ComponentShutdown args)
    {
        var query = EntityQueryEnumerator<WaveGameRuleComponent>();
        while (query.MoveNext(out var rule))
        {
            rule.SpawnerEntities.Remove(ent.Owner);
            rule.PreviousSpawnerEntities.Remove(ent.Owner);
        }
    }

    // Picks a random non-empty subset of the spawners unlocked at the current wave number.
    public void SelectSpawners(WaveGameRuleComponent comp, int players)
    {
        _spawnerBuffer.Clear();
        _secondaryBuffer.Clear();
        var query = EntityQueryEnumerator<WaveEnemySpawnerComponent>();
        while (query.MoveNext(out var spawnerUid, out var spawner))
        {
            if (comp.WaveNumber < spawner.FromWave)
                continue;

            if (spawner.Secondary)
                _secondaryBuffer.Add(spawnerUid);
            else
                _spawnerBuffer.Add(spawnerUid);
        }

        comp.PreviousSpawnerEntities.Clear();
        foreach (var previous in comp.SpawnerEntities)
        {
            if (!TryComp<WaveEnemySpawnerComponent>(previous, out var previousSpawner) || !previousSpawner.Secondary)
                comp.PreviousSpawnerEntities.Add(previous);
        }

        comp.SpawnerEntities.Clear();

        if (_spawnerBuffer.Count == 0)
            return;

        // Reroll if this wave would get the same spawner set as the one before it, so single-corridor can't chain indefinitely.
        const int maxAttempts = 20;
        var lanes = Math.Min(_spawnerBuffer.Count, MaxLanes(comp, players));
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (_spawnerBuffer.Count > 1)
                _random.Shuffle(_spawnerBuffer);

            var activeCount = RollActiveCount(lanes);
            comp.SpawnerEntities.Clear();
            for (var i = 0; i < activeCount; i++)
                comp.SpawnerEntities.Add(_spawnerBuffer[i]);

            if (!comp.PreviousSpawnerEntities.SetEquals(comp.SpawnerEntities))
                break;
        }

        // Secondaries only support a lane that's already active this wave, never an unlisted one.
        foreach (var secondary in _secondaryBuffer)
        {
            if (!TryComp<WaveEnemySpawnerComponent>(secondary, out var spawner))
                continue;

            var supported = false;
            foreach (var primary in comp.SpawnerEntities)
            {
                if (TryComp<WaveEnemySpawnerComponent>(primary, out var primarySpawner) &&
                    primarySpawner.DirectionLabel == spawner.DirectionLabel)
                {
                    supported = true;
                    break;
                }
            }

            if (supported && _random.NextFloat() < spawner.ActivationChance)
                comp.SpawnerEntities.Add(secondary);
        }
    }
    public static int MaxLanes(WaveGameRuleComponent comp, int players)
    {
        var table = comp.MaxLanesByPlayers;
        var index = Math.Max(players, 1) - 1;
        return index < table.Count ? table[index] : int.MaxValue;
    }

    private int RollActiveCount(int spawnerCount)
    {
        if (spawnerCount <= 1)
            return spawnerCount;

        var totalWeight = spawnerCount - 1 + 0.5f;
        var roll = _random.NextFloat() * totalWeight;
        return roll < 0.5f ? 1 : 2 + (int) (roll - 0.5f);
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
        if (TryComp<FSDarkWaveRuleComponent>(uid, out var dark) && dark.Active)
        {
            SpawnDarkWaveBatch(comp, dark);
            return;
        }

        if (comp.SpawnerEntities.Count == 0)
        {
            SelectSpawners(comp, comp.ScalingPlayersThisWave);
            comp.NextSpawnTime = _timing.CurTime + TimeSpan.FromSeconds(1);
            if (comp.SpawnerEntities.Count == 0)
                return;
        }

        var pool = GetDirectorPool(comp);
        var remaining = comp.EnemyTotalThisWave - comp.EnemiesSpawnedThisWave;
        BuildSpawnPlan(comp);
        var toSpawn = Math.Min(remaining, _spawnPlan.Count);

        for (var i = 0; i < toSpawn; i++)
        {
            var spawnerUid = _spawnPlan[i];

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

            if (proto == RevenantProto && CountAliveRevenants() >= MaxAliveRevenants)
                proto = _random.Pick(pool);

            if (TryGetRevenantSpawn(proto, comp.WaveNumber, out var flank))
                coords = flank;

            var enemy = SpawnWaveEnemy(proto, coords, comp);
            comp.AliveEnemies.Add(enemy);
            comp.EnemiesSpawnedThisWave++;
        }

        var intervalSec = comp.MinSpawnInterval + _random.NextFloat() * (comp.MaxSpawnInterval - comp.MinSpawnInterval);
        comp.NextSpawnTime = _timing.CurTime + TimeSpan.FromSeconds(intervalSec);
    }

    private void BuildSpawnPlan(WaveGameRuleComponent comp)
    {
        _spawnPlan.Clear();

        foreach (var spawnerUid in comp.SpawnerEntities)
        {
            var batch = comp.SpawnBatchSize;
            if (TryComp<WaveEnemySpawnerComponent>(spawnerUid, out var spawner) && spawner.Secondary)
                batch = Math.Max(1, (int) MathF.Round(comp.SpawnBatchSize * spawner.BatchMultiplier));

            for (var i = 0; i < batch; i++)
                _spawnPlan.Add(spawnerUid);
        }

        if (_spawnPlan.Count > 1)
            _random.Shuffle(_spawnPlan);
    }

    private int CountAliveRevenants()
    {
        var count = 0;
        var query = EntityQueryEnumerator<FSRevenantComponent, MobStateComponent>();
        while (query.MoveNext(out _, out _, out var mobState))
        {
            if (mobState.CurrentState != MobState.Dead)
                count++;
        }

        return count;
    }

    private bool TryGetRevenantSpawn(EntProtoId proto, int waveNumber, out EntityCoordinates coords)
    {
        coords = default;

        if (proto != RevenantProto)
            return false;

        _spawnerBuffer.Clear();
        var query = EntityQueryEnumerator<FSRevenantSpawnerComponent>();
        while (query.MoveNext(out var uid, out var spawner))
        {
            if (waveNumber >= spawner.FromWave)
                _spawnerBuffer.Add(uid);
        }

        if (_spawnerBuffer.Count == 0)
            return false;

        var chosen = _random.Pick(_spawnerBuffer);
        coords = Transform(chosen).Coordinates;

        if (!TryComp<FSRevenantSpawnerComponent>(chosen, out var chosenComp) || chosenComp.SpawnRadius <= 0f)
            return true;

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var angle = _random.NextFloat() * MathF.Tau;
            var radius = MathF.Sqrt(_random.NextFloat()) * chosenComp.SpawnRadius;
            var candidate = coords.Offset(new System.Numerics.Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius));
            if (IsSpawnClear(candidate))
            {
                coords = candidate;
                break;
            }
        }

        return true;
    }

    // Shared by the boss spawn and the regular batch spawn so the two paths can't drift apart.
    public EntityUid SpawnWaveEnemy(EntProtoId proto, EntityCoordinates coords, WaveGameRuleComponent comp)
    {
        var enemy = Spawn(proto, coords);
        EnsureComp<WaveSpawnedTagComponent>(enemy);
        if (TryComp<HTNComponent>(enemy, out var htn))
        {
            // LOS-filtered by NearbyHostilesQuery, so 15f sees down corridors but not through walls into rooms.
            htn.Blackboard.SetValue("VisionRadius", 15f);
            htn.Blackboard.SetValue("AggroVisionRadius", 15f);
            // Matches MeleeWeapon.range. TargetInRangePrecondition plans against this, and the CCC
            // is a 3x3 body, so leaving it at the 1.0 default makes the beeline branch unplannable.
            htn.Blackboard.SetValue("MeleeRange", 2.0f);
            htn.Blackboard.SetValue(NPCBlackboard.NavSmash, true);
            htn.Blackboard.SetValue(NPCBlackboard.NavPry, false);
            if (comp.CCCEntity.IsValid())
                htn.Blackboard.SetValue(NPCBlackboard.CurrentOrderedTarget, comp.CCCEntity);

            // Bloater has higher aggro radius to prioritise player hunting over CCC beeline
            if (TryComp<FSBoomOnDeathComponent>(enemy, out var boom))
            {
                htn.Blackboard.SetValue("AggroVisionRadius", boom.AggroVisionRadius);
                htn.Blackboard.SetValue("VisionRadius", boom.AggroVisionRadius);
            }
        }
        _scaling.ScaleEnemyHp(enemy, comp.WaveNumber, comp.ScalingPlayersThisWave);
        _scaling.ScaleBossForPlayers(enemy, comp.ScalingPlayersThisWave);
        _scaling.ScaleEnemySpeed(enemy, comp.WaveNumber);
        _scaling.ScaleEnemyDamage(enemy, comp.WaveNumber, comp.ScalingPlayersThisWave);
        _scaling.ScaleEnemyFireRate(enemy, comp.WaveNumber);
        // Armor system recalculates MaxArmor after the HP scale.
        var scaled = new FSEnemyHpScaledEvent();
        RaiseLocalEvent(enemy, ref scaled);
        return enemy;
    }

    private EntProtoId SelectEnemyProto(WaveGameRuleComponent comp, IReadOnlyList<EntProtoId> pool)
    {
        foreach (var special in comp.SpecialEnemyPool)
        {
            if (comp.WaveNumber >= special.FromWave && _random.NextFloat() < special.SpawnChance)
                return special.EnemyId;
        }
        return _random.Pick(pool);
    }

    private const string DarkWaveEnemyProto = "FSZombieRevenantDark";
    private const float DarkWaveSpawnRadiusMin = 4f;
    private const float DarkWaveSpawnRadiusMax = 8f;

    private readonly List<EntityCoordinates> _darkWavePlayerBuffer = new();

    private void SpawnDarkWaveBatch(WaveGameRuleComponent comp, FSDarkWaveRuleComponent dark)
    {
        comp.NextSpawnTime = _timing.CurTime + dark.SpawnInterval;
        if (comp.AliveEnemies.Count >= dark.MaxAlive)
            return;

        _darkWavePlayerBuffer.Clear();
        var playerQuery = EntityQueryEnumerator<ActorComponent, MobStateComponent>();
        while (playerQuery.MoveNext(out var playerUid, out _, out var ms))
        {
            if (HasComp<WaveSpawnedTagComponent>(playerUid)) continue;
            if (HasComp<GhostComponent>(playerUid)) continue;
            if (ms.CurrentState != MobState.Alive) continue;
            _darkWavePlayerBuffer.Add(Transform(playerUid).Coordinates);
        }

        if (_darkWavePlayerBuffer.Count == 0)
            return;

        for (var i = 0; i < comp.SpawnBatchSize; i++)
        {
            if (comp.AliveEnemies.Count >= dark.MaxAlive)
                break;

            var playerCoords = _darkWavePlayerBuffer[_random.Next(_darkWavePlayerBuffer.Count)];
            var spawnCoords = FindSpawnNearPlayer(playerCoords);
            if (spawnCoords == null) continue;

            var enemy = SpawnWaveEnemy(DarkWaveEnemyProto, spawnCoords.Value, comp);
            comp.AliveEnemies.Add(enemy);
            comp.EnemiesSpawnedThisWave++;
        }

    }

    private EntityCoordinates? FindSpawnNearPlayer(EntityCoordinates playerCoords)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var angle = _random.NextFloat() * MathF.Tau;
            var radius = DarkWaveSpawnRadiusMin + _random.NextFloat() * (DarkWaveSpawnRadiusMax - DarkWaveSpawnRadiusMin);
            var offset = new System.Numerics.Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
            var candidate = playerCoords.Offset(offset);
            if (IsSpawnClear(candidate))
                return candidate;
        }
        return null;
    }

    public string GetSpawnDirections(WaveGameRuleComponent comp)
    {
        var labels = new SortedSet<string>();
        foreach (var spawnerUid in comp.SpawnerEntities)
        {
            if (TryComp<WaveEnemySpawnerComponent>(spawnerUid, out var spawner) && spawner.DirectionLabel.Length > 0)
                labels.Add(spawner.DirectionLabel);
        }

        if (RevenantPossible(comp))
        {
            var query = EntityQueryEnumerator<FSRevenantSpawnerComponent>();
            while (query.MoveNext(out var uid, out var spawner))
            {
                if (comp.WaveNumber >= spawner.FromWave && TryGetCompassLabel(uid, out var label))
                    labels.Add(label);
            }
        }

        if (labels.Count == 0)
            return comp.SpawnerEntities.Count == 0 ? Loc.GetString("fs-ccc-spawners-none") : "?";

        return string.Join("/", labels);
    }

    private bool RevenantPossible(WaveGameRuleComponent comp)
    {
        foreach (var special in comp.SpecialEnemyPool)
        {
            if (special.EnemyId == RevenantProto && comp.WaveNumber >= special.FromWave)
                return true;
        }

        return GetDirectorPool(comp).Contains(RevenantProto);
    }

    private bool TryGetCompassLabel(EntityUid uid, out string label)
    {
        label = string.Empty;

        var query = EntityQueryEnumerator<FinalStandCCCComponent>();
        if (!query.MoveNext(out var cccUid, out _))
            return false;

        var delta = _transform.GetWorldPosition(uid) - _transform.GetWorldPosition(cccUid);
        label = MathF.Abs(delta.X) > MathF.Abs(delta.Y)
            ? delta.X > 0 ? "E" : "W"
            : delta.Y > 0 ? "N" : "S";
        return true;
    }

    public static IReadOnlyList<EntProtoId> GetDirectorPool(WaveGameRuleComponent comp)
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
