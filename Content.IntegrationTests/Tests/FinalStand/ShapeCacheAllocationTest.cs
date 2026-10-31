using System.Numerics;
using Content.Client._FinalStand.UI;
using Content.IntegrationTests.Fixtures;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.FinalStand;

// Chips, health bars and mech gauges draw dozens of baked shapes a frame. Drawing one that's already baked must not
// allocate: the string keys and closures built before the cache lookup came to megabytes a second at high frame rates.
[TestFixture]
[TestOf(typeof(FSShapes))]
public sealed class ShapeCacheAllocationTest : GameTest
{
    [Test]
    public async Task CachedShapesDrawWithoutAllocating()
    {
        await Pair.Client.WaitPost(() =>
        {
            var handle = new NullScreenHandle();

            void DrawAll()
            {
                FSShapes.Slanted(handle, new Vector2(10f, 10f), 40f, 8f, 3f, Color.White, border: 1f, fill: 0.5f);
                FSShapes.Diamond(handle, new Vector2(10f, 10f), 9f, Color.White, border: 1f);
                FSShapes.Arc(handle, new Vector2(10f, 10f), 20f, 3f, -90f, 45f, Color.White);
            }

            // The first draw bakes them.
            DrawAll();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++)
                DrawAll();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.LessThan(1024), "drawing already-baked shapes allocated");
        });
    }

    private sealed class NullScreenHandle() : DrawingHandleScreen(null)
    {
        public override void SetTransform(in Matrix3x2 matrix) { }
        public override Matrix3x2 GetTransform() => Matrix3x2.Identity;
        public override void UseShader(ShaderInstance shader) { }
        public override ShaderInstance GetShader() => null;

        public override void DrawPrimitives(DrawPrimitiveTopology primitiveTopology, Texture texture,
            ReadOnlySpan<DrawVertexUV2DColor> vertices) { }

        public override void DrawPrimitives(DrawPrimitiveTopology primitiveTopology, Texture texture,
            ReadOnlySpan<ushort> indices, ReadOnlySpan<DrawVertexUV2DColor> vertices) { }

        public override void DrawLine(Vector2 from, Vector2 to, Color color) { }
        public override void RenderInRenderTarget(IRenderTarget target, Action a, Color? clearColor) { }
        public override void DrawRect(UIBox2 rect, Color color, bool filled = true) { }

        public override void DrawTextureRectRegion(Texture texture, UIBox2 rect, UIBox2? subRegion = null,
            Color? modulate = null) { }

        public override void DrawEntity(EntityUid entity, Vector2 position, Vector2 scale, Angle? worldRot,
            Angle eyeRotation = default, Direction? overrideDirection = null, SpriteComponent sprite = null,
            TransformComponent xform = null, SharedTransformSystem xformSystem = null) { }
    }
}
