using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Ammo;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

// Losing a CCC life sends the round back to prep for the same wave without ending it. The per-wave resupplies have
// to reset on that path too, or everyone who refilled during the lost attempt goes without for the replay.
[TestFixture]
[TestOf(typeof(WaveAmmoBoxSystem))]
public sealed class WaveReplayResetTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Map = PoolManager.TestStation,
    };

    private static readonly EntProtoId WaveRule = "WaveSurvivalZombie";
    private static readonly EntProtoId AmmoBox = "WaveAmmoBox";
    private static readonly EntProtoId Human = "MobHuman";

    [Test]
    public async Task LostLifeResetsTheAmmoBoxes()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var ticker = entMan.System<GameTicker>();
        var waves = entMan.System<WaveGameRuleSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var interaction = entMan.System<SharedInteractionSystem>();

        await server.WaitPost(() =>
        {
            Assert.That(ticker.StartGameRule(WaveRule, out _));
            waves.RequestWaveStart();
        });

        await Pair.RunTicksSync(timing.TickRate * 11);

        EntityUid user = default;
        EntityUid box = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(waves.TryGetWaveStatus(out _, out var phase));
            Assert.That(phase, Is.EqualTo(WavePhase.Combat));

            var grid = entMan.EntityQueryEnumerator<MapGridComponent>();
            Assert.That(grid.MoveNext(out var gridUid, out _));
            var coords = new EntityCoordinates(gridUid, 0.5f, 0.5f);

            user = entMan.SpawnEntity(Human, coords);
            box = entMan.SpawnEntity(AmmoBox, coords);
            interaction.InteractHand(user, box);
        });

        await Pair.RunTicksSync(timing.TickRate * 5);

        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<WaveAmmoBoxComponent>(box);
            Assert.That(comp.UsedBy, Does.Contain(user), "the refill didn't go through");

            Assert.That(waves.FailWave(), Is.EqualTo(1));
            Assert.That(comp.UsedBy, Is.Empty, "the replayed wave kept last attempt's ammo box use");
        });
    }
}
