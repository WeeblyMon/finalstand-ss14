using Content.IntegrationTests.Fixtures;
using Content.Client._FinalStand.Deployables;
using Content.Shared._FinalStand.Deployables;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// The owner's low-ammo alarm pulses a sentry's chip until it's refilled. Nothing tells the client about a refill, so
// the alarm has to follow the sentry's replicated ammo; it used to pulse until the sentry was gone.
[TestFixture]
[TestOf(typeof(FSDeployableAlertClientSystem))]
public sealed class SentryLowAlarmTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true };

    private const string Sentry = "FSSentryTurretDeployed";

    [Test]
    public async Task RefillClearsTheAlarm()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        EntityUid sentry = default;
        await server.WaitPost(() =>
        {
            sentry = sEntMan.SpawnEntity(Sentry, map.GridCoords);
            var turret = sEntMan.GetComponent<FSSentryTurretComponent>(sentry);
            turret.Ammo = 1;
            sEntMan.Dirty(sentry, turret);
        });

        await Pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var alerts = cEntMan.System<FSDeployableAlertClientSystem>();
            alerts.Low.Add(Pair.ToClientUid(sentry));
            Assert.That(alerts.Pulse(Pair.ToClientUid(sentry)), Is.GreaterThan(0f), "a low sentry's alarm didn't pulse");
        });

        await server.WaitPost(() =>
        {
            var turret = sEntMan.GetComponent<FSSentryTurretComponent>(sentry);
            turret.Ammo = turret.MaxAmmo;
            sEntMan.Dirty(sentry, turret);
        });

        await Pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var alerts = cEntMan.System<FSDeployableAlertClientSystem>();
            Assert.That(alerts.Pulse(Pair.ToClientUid(sentry)), Is.Zero, "a refilled sentry kept its low-ammo alarm");
        });
    }
}
