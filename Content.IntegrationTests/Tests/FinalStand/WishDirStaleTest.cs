#nullable enable
using System;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// A conveyor adds the way a mob is trying to walk to its push. Once the mob can't move (stunned, crit, pulled) it
// isn't trying to walk anywhere, so the last direction it walked in mustn't be left behind for the belt.
[TestFixture]
[TestOf(typeof(SharedMoverController))]
public sealed class WishDirStaleTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task StunnedMobWishesForNothing()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mover = entMan.System<SharedMoverController>();
        var stun = entMan.System<SharedStunSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid mob = default;
        await server.WaitPost(() =>
        {
            mob = entMan.SpawnEntity("MobHuman", map.GridCoords);
            entMan.EnsureComponent<ActiveInputMoverComponent>(mob);

            // As if it had been sprinting east along a belt.
            mover.SetWishDir((mob, entMan.GetComponent<InputMoverComponent>(mob)), new Vector2(4.5f, 0f));
            Assert.That(stun.TryUpdateStunDuration(mob, TimeSpan.FromSeconds(5)));
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
            Assert.That(mover.GetWishDir(mob), Is.EqualTo(Vector2.Zero), "a stunned mob still wished to walk"));
    }
}
