using Content.IntegrationTests.Fixtures;
using System.Collections.Generic;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Teleportation.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

// Runtime deadlines move with a pause. While a map is paused its entities don't update but the clock runs on, so a
// fixed deadline expired the moment an admin unpaused: every placed null field and damage beacon vanished at once.
[TestFixture]
[TestOf(typeof(FSDeployableLifetimeComponent))]
[TestOf(typeof(FSUntouchableComponent))]
[TestOf(typeof(FSMedicalBonusSystem))]
[TestOf(typeof(HandTeleporterComponent))]
[TestOf(typeof(FSTreatmentAttributionComponent))]
public sealed class PausedDeadlineTest : GameTest
{
    private const string NullField = "FSNullFieldDeployed";

    [Test]
    public async Task PauseDoesNotUseUpALifetime()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid field = default;
        var left = TimeSpan.Zero;
        await server.WaitPost(() =>
        {
            field = entMan.SpawnEntity(NullField, map.GridCoords);
            left = entMan.GetComponent<FSDeployableLifetimeComponent>(field).ExpiresAt - timing.CurTime;
            mapSystem.SetPaused(map.MapId, true);
        });

        Assert.That(left, Is.GreaterThan(TimeSpan.Zero), "the null field had no lifetime to spend");

        // Paused for longer than the field lasts.
        await server.WaitRunTicks((int) (timing.TickRate * (left.TotalSeconds + 2)));
        await server.WaitPost(() => mapSystem.SetPaused(map.MapId, false));
        await server.WaitRunTicks(30);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(field) && !entMan.IsQueuedForDeletion(field),
                "the null field expired while its map was paused");
            var stillLeft = entMan.GetComponent<FSDeployableLifetimeComponent>(field).ExpiresAt - timing.CurTime;
            Assert.That(stillLeft, Is.GreaterThan(left - TimeSpan.FromSeconds(2)), "the pause used up the field's lifetime");
        });
    }

    // A deadline that isn't running stays unset through a pause. Shifted like a real one, Untouchable's "no recharge
    // pending" became a recharge already due, and a full player got a free charge on the next hit.
    [Test]
    public async Task UnsetDeadlineStaysUnset()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid player = default;
        await server.WaitPost(() =>
        {
            player = entMan.SpawnEntity("MobHuman", map.GridCoords);
            entMan.EnsureComponent<FSUntouchableComponent>(player).CurrentCharges = 1;
            mapSystem.SetPaused(map.MapId, true);
        });

        await server.WaitRunTicks(30);
        await server.WaitPost(() => mapSystem.SetPaused(map.MapId, false));

        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<FSUntouchableComponent>(player).NextChargeTime, Is.Null,
                "the pause turned an unset recharge into one already due"));
    }

    // A CMO buff's deadline lives inside the buff, where the generated pause handling can't see it.
    [Test]
    public async Task PauseDoesNotUseUpAMedicalBuff()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var bonus = entMan.System<FSMedicalBonusSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid player = default;
        await server.WaitPost(() =>
        {
            player = entMan.SpawnEntity("MobHuman", map.GridCoords);
            bonus.ApplyBuff(player, "test", new Dictionary<FSMedicalBonusCategory, float>
            {
                [FSMedicalBonusCategory.Movement] = 0.1f,
            }, TimeSpan.FromSeconds(1));
            mapSystem.SetPaused(map.MapId, true);
        });

        // Paused for longer than the buff lasts.
        await server.WaitRunTicks((int) (timing.TickRate * 2));
        await server.WaitPost(() => mapSystem.SetPaused(map.MapId, false));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
            Assert.That(bonus.GetBonus(player, FSMedicalBonusCategory.Movement), Is.GreaterThan(0f),
                "the pause used up the medical buff"));
    }

    // A hand teleporter's portals collapse together at one deadline, kept on the teleporter.
    [Test]
    public async Task PauseDoesNotUseUpPortals()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var map = await Pair.CreateTestMap();

        HandTeleporterComponent teleporter = null;
        await server.WaitPost(() =>
        {
            teleporter = entMan.GetComponent<HandTeleporterComponent>(entMan.SpawnEntity("HandTeleporter", map.GridCoords));
            teleporter.PortalsExpireAt = timing.CurTime + TimeSpan.FromSeconds(1);
            mapSystem.SetPaused(map.MapId, true);
        });

        // Paused for longer than the portals last.
        await server.WaitRunTicks((int) (timing.TickRate * 2));
        await server.WaitPost(() => mapSystem.SetPaused(map.MapId, false));

        await server.WaitAssertion(() =>
            Assert.That(teleporter.PortalsExpireAt, Is.GreaterThan(timing.CurTime), "the pause used up the portals' time"));
    }

    // A medic keeps the credit for a patient they treated for a while after; a pause doesn't eat into that.
    [Test]
    public async Task PauseDoesNotUseUpTreatmentCredit()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var map = await Pair.CreateTestMap();

        FSTreatmentAttributionComponent attribution = null;
        EntityUid medicMind = default;
        var creditEnds = TimeSpan.Zero;
        await server.WaitPost(() =>
        {
            // Recording a treatment needs a connected medic; the window it leaves behind is all that matters here.
            medicMind = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var patient = entMan.SpawnEntity("MobHuman", map.GridCoords);
            attribution = entMan.EnsureComponent<FSTreatmentAttributionComponent>(patient);
            creditEnds = timing.CurTime + TimeSpan.FromSeconds(1);
            var treaters = attribution.RecentTreaters;
            treaters[medicMind] = creditEnds;
            mapSystem.SetPaused(map.MapId, true);
        });

        var paused = TimeSpan.FromSeconds(2);
        await server.WaitRunTicks((int) (timing.TickRate * paused.TotalSeconds));
        await server.WaitPost(() => mapSystem.SetPaused(map.MapId, false));

        await server.WaitAssertion(() =>
        {
            var treaters = attribution.RecentTreaters;
            Assert.That(treaters[medicMind], Is.GreaterThanOrEqualTo(creditEnds + paused - TimeSpan.FromSeconds(0.1)),
                "the pause used up the medic's credit window");
        });
    }
}
