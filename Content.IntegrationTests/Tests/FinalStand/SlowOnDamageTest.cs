#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

// IgnoreSlowOnDamage sitting on the mob itself, as wave enemies have it from their prototype, has to stop damage
// slowing it, and give the slowdown back if it goes.
[TestFixture]
[TestOf(typeof(SlowOnDamageSystem))]
public sealed class SlowOnDamageTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    [Test]
    public async Task DamagedZombieKeepsItsSpeed()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var damageable = entMan.System<DamageableSystem>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var zombie = entMan.SpawnEntity("FSZombieNormal", map.GridCoords);
            var speed = entMan.GetComponent<MovementSpeedModifierComponent>(zombie);
            var full = speed.CurrentSprintSpeed;

            // Past the first slowdown threshold (60), short of dead. Anything else changing its speed (a slow, the
            // cold) refreshes the lot, so refresh as well.
            damageable.TryChangeDamage(zombie, new DamageSpecifier(protos.Index(Blunt), 65), true);
            Assert.That(entMan.GetComponent<DamageableComponent>(zombie).TotalDamage.Float(), Is.GreaterThanOrEqualTo(60f));
            entMan.System<MovementSpeedModifierSystem>().RefreshMovementSpeedModifiers(zombie);
            Assert.That(speed.CurrentSprintSpeed, Is.EqualTo(full).Within(0.001f), "a damaged wave zombie slowed down");
        });
    }

    [Test]
    public async Task IgnoringLiftsTheSlowdownWhileItIsThere()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var damageable = entMan.System<DamageableSystem>();
        var protos = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        // All in one tick, so nothing else on the airless test map gets a turn to change the speed.
        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var speed = entMan.GetComponent<MovementSpeedModifierComponent>(human);
            var full = speed.CurrentSprintSpeed;

            damageable.TryChangeDamage(human, new DamageSpecifier(protos.Index(Blunt), 65), true);
            var slowed = speed.CurrentSprintSpeed;
            Assert.That(slowed, Is.LessThan(full), "the human should be slowed by the damage");

            entMan.AddComponent<IgnoreSlowOnDamageComponent>(human);
            Assert.That(speed.CurrentSprintSpeed, Is.EqualTo(full).Within(0.001f), "ignoring didn't lift the slowdown");

            entMan.RemoveComponent<IgnoreSlowOnDamageComponent>(human);
            Assert.That(speed.CurrentSprintSpeed, Is.EqualTo(slowed).Within(0.001f), "the slowdown didn't come back");
        });
    }
}
