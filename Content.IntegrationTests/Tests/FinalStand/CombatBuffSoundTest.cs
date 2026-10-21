using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.EntityEffects;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// A combat buff lands from metabolism, which clients predict and re-run on every state they get until the server
// catches up. Its sound comes from the server only; played on the client too, everyone nearby heard it stacked.
[TestFixture]
[TestOf(typeof(FSApplyCombatBuffSystem))]
public sealed class CombatBuffSoundTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true };

    [Test]
    public async Task ClientPredictionPlaysNoSound()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        EntityUid human = default;
        await server.WaitPost(() =>
        {
            human = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            sEntMan.EnsureComponent<FSFriendlyFireComponent>(human);
        });

        await Pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var target = Pair.ToClientUid(human);
            Assert.That(cEntMan.HasComponent<FSFriendlyFireComponent>(target), "the client never got the body");

            var before = cEntMan.EntityQuery<AudioComponent>().Count();
            var buff = new FSApplyCombatBuff
            {
                Bonuses = new Dictionary<FSMedicalBonusCategory, float> { [FSMedicalBonusCategory.Movement] = 0.1f },
            };
            cEntMan.System<SharedEntityEffectsSystem>().ApplyEffect(target, buff);

            Assert.That(cEntMan.EntityQuery<AudioComponent>().Count(), Is.EqualTo(before),
                "the client played the buff sound itself");
        });
    }
}
