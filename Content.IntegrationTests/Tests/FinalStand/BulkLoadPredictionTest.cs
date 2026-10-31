using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.FinalStand;

// Loading a stripper clip is predicted, so the client runs the transfer too and spawns the clip's rounds itself.
// Those are stand-ins until the server's container state arrives; any left behind pile up all round.
[TestFixture]
[TestOf(typeof(FSBulkLoaderSystem))]
public sealed class BulkLoadPredictionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true };

    private const string Rifle = "WeaponSniperMosin";
    private const string Clip = "FSStripperClipMosin";
    private const string Round = "FSCartridgeMosin";

    [Test]
    public async Task PredictedClipLoadLeavesNoClientRounds()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        EntityUid rifle = default;
        EntityUid clip = default;
        EntityUid player = default;
        await server.WaitPost(() =>
        {
            player = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Pair.Server.ResolveDependency<Robust.Server.Player.IPlayerManager>().SetAttachedEntity(Pair.Player!, player);

            rifle = sEntMan.SpawnEntity(Rifle, map.GridCoords);
            clip = sEntMan.SpawnEntity(Clip, map.GridCoords);

            // Empty the rifle so the whole clip fits.
            var taken = new List<(EntityUid? Entity, IShootable Shootable)>();
            sEntMan.EventBus.RaiseLocalEvent(rifle, new TakeAmmoEvent(10, taken, map.GridCoords, null));
            foreach (var (round, _) in taken)
            {
                if (round != null)
                    sEntMan.DeleteEntity(round.Value);
            }
        });

        await Pair.RunTicksSync(10);

        await client.WaitPost(() =>
        {
            var loader = cEntMan.System<FSBulkLoaderSystem>();
            Assert.That(loader.BulkTransfer(Pair.ToClientUid(clip), Pair.ToClientUid(rifle), Pair.ToClientUid(player)),
                Is.GreaterThan(0), "the predicted load moved nothing");
        });

        await Pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var leftover = 0;
            var query = cEntMan.AllEntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var meta))
            {
                if (meta.EntityPrototype?.ID == Round && cEntMan.IsClientSide(uid))
                    leftover++;
            }

            Assert.That(leftover, Is.Zero, "the predicted load left client-side rounds behind");
        });
    }
}
