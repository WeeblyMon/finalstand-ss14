using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Server.GameTicking;
using Prometheus;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

// Not a pass/fail test: spawns a horde on the real map and prints server tick cost, overall and per system.
// Run with: dotnet test Content.IntegrationTests --filter "FullyQualifiedName~HordeBenchmark"
[TestFixture]
[Explicit]
public sealed class HordeBenchmark : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Map = "FinalStandMap1",
    };

    private static readonly EntProtoId WaveRule = "WaveSurvivalZombie";
    private static readonly EntProtoId[] Horde = ["FSZombieNormal", "FSZombieRunner"];

    private const int Wave = 30;
    private const int WarmupTicks = 120;
    private const int MeasuredTicks = 500;
    private const int Chunks = 5;

    private static readonly Regex SumLine = new(@"^robust_entity_systems_update_usage_sum\{system=""([^""]+)""\} (\S+)$", RegexOptions.Multiline);

    [Test]
    [TestCase(500)]
    [TestCase(1000)]
    public async Task HordeTickCost(int count)
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var ticker = entMan.System<GameTicker>();
        var waves = entMan.System<WaveGameRuleSystem>();
        var spawning = entMan.System<WaveEnemySpawningSystem>();
        var random = server.ResolveDependency<IRobustRandom>();
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitPost(() =>
        {
            Assert.That(ticker.StartGameRule(WaveRule, out var rule));
            Assert.That(waves.ForceNextWave(Wave), Is.EqualTo(Wave));

            var spawners = new List<EntityCoordinates>();
            var query = entMan.EntityQueryEnumerator<WaveEnemySpawnerComponent, TransformComponent>();
            while (query.MoveNext(out _, out var xform))
                spawners.Add(xform.Coordinates);
            Assert.That(spawners, Is.Not.Empty, "the map has no wave spawners");

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
        var chunks = new List<double>();
        for (var i = 0; i < Chunks; i++)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await Pair.RunTicksSync(MeasuredTicks / Chunks);
            chunks.Add(stopwatch.Elapsed.TotalMilliseconds / (MeasuredTicks / Chunks));
        }
        var after = await ReadSystemTotals();

        chunks.Sort();
        var budget = 1000.0 / timing.TickRate;
        TestContext.Out.WriteLine($"{count} zombies, wave {Wave}: median {chunks[Chunks / 2]:F2} ms/tick, " +
                                  $"best {chunks[0]:F2}, worst {chunks[^1]:F2} (budget {budget:F1} ms at {timing.TickRate} TPS)");

        var deltas = after
            .Select(kv => (System: kv.Key, Ms: (kv.Value - before.GetValueOrDefault(kv.Key)) * 1000.0 / MeasuredTicks))
            .OrderByDescending(x => x.Ms)
            .Take(25);

        foreach (var (system, ms) in deltas)
            TestContext.Out.WriteLine($"  {ms,8:F3} ms  {system}");

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
