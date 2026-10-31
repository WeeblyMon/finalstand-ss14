#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Actions;
using Content.Shared.Actions;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class GrantedActionsTest : GameTest
{
    private const string Mob = "MobHuman";
    private const string Action = "FSMedicPingAction";

    // A job or perk action is handed out once per body, however often the grant runs, and can be taken back.
    [Test]
    public async Task GrantIsOncePerMobAndRevocable()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var grants = entMan.System<FSGrantedActionsSystem>();
            var actions = entMan.System<SharedActionsSystem>();
            var mob = entMan.SpawnEntity(Mob, new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            var before = actions.GetActions(mob).Count();

            grants.Grant(mob, Action);
            grants.Grant(mob, Action);

            Assert.Multiple(() =>
            {
                Assert.That(grants.Has(mob, Action));
                Assert.That(actions.GetActions(mob).Count(), Is.EqualTo(before + 1), "a repeat grant added a second action");
            });

            grants.Revoke(mob, Action);

            Assert.Multiple(() =>
            {
                Assert.That(grants.Has(mob, Action), Is.False);
                Assert.That(actions.GetActions(mob).Count(), Is.EqualTo(before));
            });
        });
    }
}
