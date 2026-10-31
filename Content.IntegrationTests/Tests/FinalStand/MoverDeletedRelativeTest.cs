using System;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.FinalStand;

// A mover keeps its old relative grid or map for a second after moving to a new one, and that old one can be deleted
// meanwhile (dungeon fill deletes its temporary map). Sending the mover's state then named a deleted entity, which
// logged an error for every connected player.
[TestFixture]
[TestOf(typeof(SharedMoverController))]
public sealed class MoverDeletedRelativeTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true };

    [Test]
    public async Task DeletedRelativeIsNotSent()
    {
        var sEntMan = Pair.Server.EntMan;
        var map = await Pair.CreateTestMap();

        NetEntity netMob = default;
        await Pair.Server.WaitPost(() =>
        {
            var mob = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            netMob = sEntMan.GetNetEntity(mob);

            // As just after a grid change on the same map: the old relative is kept until the lerp is over.
            var gone = sEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var mover = sEntMan.GetComponent<InputMoverComponent>(mob);
            mover.RelativeEntity = gone;
            mover.LerpTarget = Pair.Server.Timing.CurTime + TimeSpan.FromSeconds(1);
            sEntMan.DeleteEntity(gone);
            sEntMan.Dirty(mob, mover);
        });

        // The error log alone fails the test; the client should also be left with no relative rather than a dangling one.
        await Pair.RunTicksSync(5);

        await Pair.Client.WaitAssertion(() =>
        {
            var cEntMan = Pair.Client.EntMan;
            var mob = cEntMan.GetEntity(netMob);
            var relative = cEntMan.GetComponent<InputMoverComponent>(mob).RelativeEntity;
            Assert.That(relative == null || cEntMan.EntityExists(relative), "the client was sent a deleted relative");
        });
    }
}
