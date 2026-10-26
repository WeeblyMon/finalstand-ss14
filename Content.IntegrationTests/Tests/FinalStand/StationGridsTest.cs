#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Components;
using Content.Shared.Station.Components;
using Content.Shared.Cargo.Components;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.Tests.FinalStand;

// The station brings only what cargo needs: its shuttle and the Automated Trading Station to fly it to. The ruins,
// debris and asteroid field of the vanilla shuttles base would sit round the station costing server time.
[TestFixture]
public sealed class StationGridsTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        InLobby = true,
        Map = "FinalStandMap1",
    };

    private const string Station = "FinalStandStation";

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), true)]
    public async Task TheStationSpawnsOnlyTheCargoShuttleAndTradingStation()
    {
        var ticker = SEntMan.System<GameTicker>();

        await Server.WaitPost(() => ticker.StartRound(force: true));
        await Pair.RunTicksSync(10);

        await Server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

            HashSet<EntityUid>? stationGrids = null;
            var stations = SEntMan.EntityQueryEnumerator<StationDataComponent, MetaDataComponent>();
            while (stations.MoveNext(out _, out var data, out var meta))
            {
                if (meta.EntityPrototype?.ID == Station)
                    stationGrids = data.Grids;
            }

            Assert.That(stationGrids, Is.Not.Null, "no Final Stand station");
            var map = SEntMan.GetComponent<TransformComponent>(stationGrids!.First()).MapID;

            var gridsOnMap = new List<EntityUid>();
            var query = SEntMan.EntityQueryEnumerator<MapGridComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var xform))
            {
                if (xform.MapID == map)
                    gridsOnMap.Add(uid);
            }

            Assert.Multiple(() =>
            {
                Assert.That(stationGrids, Has.Count.EqualTo(3), "expected the station, its cargo shuttle and the trading station");
                Assert.That(stationGrids.Count(g => SEntMan.HasComponent<TradeStationComponent>(g)), Is.EqualTo(1),
                    "no trading station");
                Assert.That(stationGrids.Count(g => SEntMan.HasComponent<ShuttleComponent>(g)
                    && !SEntMan.HasComponent<TradeStationComponent>(g)), Is.GreaterThanOrEqualTo(1), "no cargo shuttle");
                Assert.That(gridsOnMap, Is.SubsetOf(stationGrids), "other grids were spawned round the station");
            });
        });
    }
}
