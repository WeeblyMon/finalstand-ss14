using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Shared.Damage.Systems;
using Prometheus;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

// Not a pass/fail test: spawns a horde on the real map and prints tick cost, overall and per system.
// Defenders are godmode humans standing by the spawners so the horde fights instead of just walking;
// the test client controls the first one, so its cost covers receiving and applying the horde's state.
// Run with: dotnet test Content.IntegrationTests --filter "FullyQualifiedName~HordeBenchmark"
[TestFixture]
[Explicit]
public sealed class HordeBenchmark : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        Connected = true,
        DummyTicker = false,
        Map = "FinalStandMap1",
    };

    private static readonly EntProtoId WaveRule = "WaveSurvivalZombie";
    private static readonly EntProtoId[] Horde = ["FSZombieNormal", "FSZombieRunner"];
    private static readonly EntProtoId Defender = "MobHuman";

    private const int Wave = 30;
    private const int WarmupTicks = 120;
    private const int MeasuredTicks = 500;
    private const int Chunks = 5;

    private static readonly Regex SumLine = new(@"^robust_entity_systems_update_usage_sum\{system=""([^""]+)""\} (\S+)$", RegexOptions.Multiline);

    [Test]
    [TestCase(500, 0)]
    [TestCase(500, 8)]
    [TestCase(1000, 8)]
    public async Task HordeTickCost(int count, int defenders)
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var ticker = entMan.System<GameTicker>();
        var waves = entMan.System<WaveGameRuleSystem>();
        var spawning = entMan.System<WaveEnemySpawningSystem>();
        var random = server.ResolveDependency<IRobustRandom>();
        var timing = server.ResolveDependency<IGameTiming>();
        var mind = entMan.System<MindSystem>();
        var godmode = entMan.System<SharedGodmodeSystem>();
        var client = Pair.Client;

        await server.WaitPost(() =>
        {
            Assert.That(ticker.StartGameRule(WaveRule, out var rule));
            Assert.That(waves.ForceNextWave(Wave), Is.EqualTo(Wave));

            var spawners = new List<EntityCoordinates>();
            var query = entMan.EntityQueryEnumerator<WaveEnemySpawnerComponent, TransformComponent>();
            while (query.MoveNext(out _, out var xform))
                spawners.Add(xform.Coordinates);
            Assert.That(spawners, Is.Not.Empty, "the map has no wave spawners");

            for (var i = 0; i < defenders; i++)
            {
                var defender = entMan.SpawnEntity(Defender, spawners[i % spawners.Count].Offset(random.NextVector2(3f)));
                godmode.EnableGodmode(defender);
                if (i == 0)
                    mind.ControlMob(Pair.Player!.UserId, defender);
            }

            var comp = entMan.GetComponent<WaveGameRuleComponent>(rule);
            for (var i = 0; i < count; i++)
            {
                var at = random.Pick(spawners).Offset(random.NextVector2(2f));
                spawning.SpawnWaveEnemy(random.Pick(Horde), at, comp);
            }

            entMan.EntitySysManager.MetricsEnabled = true;
        });

        await Pair.RunTicksSync(WarmupTicks);

        // Median of several chunks, since GC pauses and the client thread make single runs noisy.
        var before = await ReadSystemTotals();
        // Entities created mid-fight are sent to every client in range, so count what keeps spawning.
        var spawned = new Dictionary<string, int>();
        void CountSpawn(Entity<MetaDataComponent> ent)
        {
            var id = ent.Comp.EntityPrototype?.ID ?? "(none)";
            if (entMan.TryGetComponent<AudioComponent>(ent, out var audio))
                id = $"{id} {audio.FileName}";
            spawned[id] = spawned.GetValueOrDefault(id) + 1;
        }
        entMan.EntityInitialized += CountSpawn;

        var allocatedBefore = System.GC.GetTotalAllocatedBytes();
        var gen0Before = System.GC.CollectionCount(0);
        var gen2Before = System.GC.CollectionCount(2);

        var serverChunks = new List<double>();
        var clientChunks = new List<double>();
        const int perChunk = MeasuredTicks / Chunks;
        for (var i = 0; i < Chunks; i++)
        {
            double serverMs = 0, clientMs = 0;
            for (var t = 0; t < perChunk; t++)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                await server.WaitRunTicks(1);
                serverMs += stopwatch.Elapsed.TotalMilliseconds;
                stopwatch.Restart();
                await client.WaitRunTicks(1);
                clientMs += stopwatch.Elapsed.TotalMilliseconds;
            }

            serverChunks.Add(serverMs / perChunk);
            clientChunks.Add(clientMs / perChunk);
        }
        var after = await ReadSystemTotals();
        entMan.EntityInitialized -= CountSpawn;
        var allocatedPerTick = (System.GC.GetTotalAllocatedBytes() - allocatedBefore) / (double) MeasuredTicks / 1024;
        var gen0 = System.GC.CollectionCount(0) - gen0Before;
        var gen2 = System.GC.CollectionCount(2) - gen2Before;

        serverChunks.Sort();
        clientChunks.Sort();
        var budget = 1000.0 / timing.TickRate;
        TestContext.Out.WriteLine($"{count} zombies, {defenders} defenders, wave {Wave}: server median {serverChunks[Chunks / 2]:F2} ms/tick " +
                                  $"(best {serverChunks[0]:F2}, worst {serverChunks[^1]:F2}, budget {budget:F1} ms at {timing.TickRate} TPS), " +
                                  $"client median {clientChunks[Chunks / 2]:F2} ms/tick");
        TestContext.Out.WriteLine($"Allocated {allocatedPerTick:F0} KiB/tick (server and client), GCs: {gen0} gen0, {gen2} gen2");

        var deltas = after
            .Select(kv => (System: kv.Key, Ms: (kv.Value - before.GetValueOrDefault(kv.Key)) * 1000.0 / MeasuredTicks))
            .OrderByDescending(x => x.Ms)
            .Take(25);

        foreach (var (system, ms) in deltas)
            TestContext.Out.WriteLine($"  {ms,8:F3} ms  {system}");

        TestContext.Out.WriteLine($"Spawned while measuring: {spawned.Values.Sum()}");
        foreach (var (id, n) in spawned.OrderByDescending(kv => kv.Value).Take(15))
            TestContext.Out.WriteLine($"  {n,6}  {id}");

        await server.WaitPost(() => entMan.EntitySysManager.MetricsEnabled = false);
    }

    private static async Task<Dictionary<string, double>> ReadSystemTotals()
    {
        using var stream = new MemoryStream();
        await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);
        var text = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        var totals = new Dictionary<string, double>();
        foreach (Match match in SumLine.Matches(text))
            totals[match.Groups[1].Value] = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);

        return totals;
    }
}
