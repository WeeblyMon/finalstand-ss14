#nullable enable
using System.Numerics;
using Content.Client._FinalStand.Particles;
using Content.Client._FinalStand.Visuals;
using Content.IntegrationTests.Fixtures;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.Mobs;
using Robust.Client.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.FinalStand;

// Clients draw a firing flamer's fire themselves from its firing flag and facing. The particles stop at a wall the
// same way the server's flames do, so the fire never shows on the far side of one.
[TestFixture]
[TestOf(typeof(FSFlamethrowerVisualsSystem))]
[TestOf(typeof(FSParticleSystem))]
public sealed class FlamethrowerVisualsTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private const string FlameEmitter = "FSFlamethrowerFlame";
    private const float WallFace = 3f;

    [Test]
    public async Task FlameParticlesStopAtTheWall()
    {
        var sEntMan = Server.ResolveDependency<IEntityManager>();
        var cEntMan = Client.ResolveDependency<IEntityManager>();
        var mapSystem = sEntMan.System<SharedMapSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid flamer = default;
        var flameSpeed = 0f;
        await Server.WaitPost(() =>
        {
            for (var x = -2; x <= 6; x++)
            {
                mapSystem.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            }

            // The client's eye sits just behind the flamer, so its emitter is on screen.
            var viewer = sEntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, new Vector2(-1.5f, 0.5f)));
            Server.ResolveDependency<IPlayerManager>().SetAttachedEntity(Pair.Player!, viewer);

            flamer = sEntMan.SpawnEntity("FSZombieFlamethrower", new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            sEntMan.SpawnEntity("WallSolid", new EntityCoordinates(map.Grid, new Vector2(WallFace + 0.5f, 0.5f)));
            var target = sEntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, new Vector2(5.5f, 0.5f)));

            sEntMan.RemoveComponent<HTNComponent>(flamer);
            var comp = sEntMan.GetComponent<FSFlamethrowerComponent>(flamer);
            comp.Target = target;
            comp.FiringDirection = Vector2.UnitX;
            comp.IsFiring = true;
            sEntMan.Dirty(flamer, comp);
            flameSpeed = comp.FireProjectileSpeed;
        });

        await Pair.RunTicksSync(20);

        await Client.WaitAssertion(() =>
        {
            var clientFlamer = Pair.ToClientUid(flamer);
            Assert.That(cEntMan.System<FSFlamethrowerVisualsSystem>().IsBurning(clientFlamer),
                "the client didn't start the flames");
            // It arrived already firing, before its sprite was initialized.
            Assert.That(cEntMan.System<SpriteSystem>().TryGetPostShader(cEntMan.GetComponent<SpriteComponent>(clientFlamer),
                FSPostShaders.ZombieGlow, out _), "the firing glow is missing");

            var particles = cEntMan.System<FSParticleSystem>();
            var count = 0;
            var furthest = float.MinValue;
            var fastest = 0f;
            for (var i = 0; i < particles.ParticleCount; i++)
            {
                ref var p = ref particles.Particles[i];
                if (p.Emitter.ID != FlameEmitter)
                    continue;

                count++;
                furthest = MathF.Max(furthest, p.Position.X);
                fastest = MathF.Max(fastest, p.Velocity.Length());
            }

            Assert.Multiple(() =>
            {
                Assert.That(count, Is.GreaterThan(0), "no flame particles");
                Assert.That(furthest, Is.GreaterThan(1.5f), "the flames never left the nozzle");
                Assert.That(furthest, Is.LessThanOrEqualTo(WallFace), "flame particles went into the wall");
                // The particles in flight move at the flamer's own flame speed, so they reach as far as its fire.
                Assert.That(fastest, Is.InRange(flameSpeed * 0.9f, flameSpeed * 1.1f), "flame particles don't match the flamer's speed");
            });
        });
    }
}
