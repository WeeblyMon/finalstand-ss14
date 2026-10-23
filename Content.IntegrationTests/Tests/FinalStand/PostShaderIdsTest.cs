#nullable enable
using System;
using Content.Client._FinalStand.Mobs;
using Content.Client.Graphics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.Mobs;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

// FS glows and outlines each keep their own post-shader slot on a sprite. Setting one used to clear the whole list,
// so a zombie lighting up under the cursor lost its hover outline (and stealth or floor occlusion with it).
[TestFixture]
[TestOf(typeof(FSRevenantMarkVisualsSystem))]
public sealed class PostShaderIdsTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true };

    [Test]
    public async Task GlowLeavesTheHoverOutline()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        EntityUid mob = default;
        await server.WaitPost(() => mob = sEntMan.SpawnEntity("FSZombieNormal", map.GridCoords));
        await Pair.RunTicksSync(5);

        // What the hover outline puts on.
        await client.WaitPost(() =>
        {
            var sprites = client.System<SpriteSystem>();
            var outline = client.ResolveDependency<IPrototypeManager>().Index<ShaderPrototype>("SelectionOutline").InstanceUnique();
            var sprite = client.EntMan.GetComponent<SpriteComponent>(Pair.ToClientUid(mob));
            sprites.SetPostShader(sprite, new SpriteComponent.PostShaderArgs(ContentPostShaderIds.InteractionOutline, outline)
            {
                After = ContentPostShaderIds.AfterBaseEffects,
            });
        });

        // Long-lived, or the revenant system clears it before it ever reaches the client.
        await server.WaitPost(() =>
            sEntMan.AddComponent<FSRevenantMarkedComponent>(mob).ExpiresAt = server.Timing.CurTime + TimeSpan.FromHours(1));
        await Pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var sprites = client.System<SpriteSystem>();
            var sprite = client.EntMan.GetComponent<SpriteComponent>(Pair.ToClientUid(mob));
            Assert.That(sprites.TryGetPostShader(sprite, "fs-revenant-mark", out _), "the mark never lit up");
            Assert.That(sprites.TryGetPostShader(sprite, ContentPostShaderIds.InteractionOutline, out _),
                "the mark's glow wiped the hover outline");
        });
    }
}
