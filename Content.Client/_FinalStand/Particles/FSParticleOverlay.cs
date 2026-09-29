using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Particles;

public sealed class FSParticleOverlay : Overlay
{
    private readonly FSParticleSystem _particles;
    private readonly ShaderInstance _additive;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public FSParticleOverlay(IEntityManager entManager, FSParticleSystem particles)
    {
        _particles = particles;
        _additive = IoCManager.Resolve<IPrototypeManager>().Index<ShaderPrototype>("FSAdditive").Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_particles.Count == 0)
            return;

        var handle = args.WorldHandle;
        var bounds = args.WorldAABB.Enlarged(1f);
        handle.UseShader(_additive);

        for (var i = 0; i < _particles.Count; i++)
        {
            ref var p = ref _particles.Particles[i];
            if (p.Map != args.MapId || !bounds.Contains(p.Position))
                continue;

            var t = p.Age / p.Life;
            var emitter = p.Emitter;
            var alpha = 1f;
            if (emitter.FadeIn > 0f && t < emitter.FadeIn)
                alpha = t / emitter.FadeIn;
            else if (emitter.FadeOut > 0f && t > 1f - emitter.FadeOut)
                alpha = (1f - t) / emitter.FadeOut;

            var size = p.Size0 + (p.Size1 - p.Size0) * t;
            var half = new Vector2(size, size) / 2f;
            var rotation = p.Rotation;

            if (emitter.AlignToVelocity && p.Velocity.LengthSquared() > 0.0001f)
            {
                rotation = MathF.Atan2(p.Velocity.Y, p.Velocity.X);
                half.X *= emitter.Stretch;
            }

            var box = new Box2(p.Position - half, p.Position + half);
            handle.DrawTextureRect(p.Texture, new Box2Rotated(box, rotation, p.Position), p.Color.WithAlpha(p.Color.A * alpha));
        }

        handle.UseShader(null);
    }
}
