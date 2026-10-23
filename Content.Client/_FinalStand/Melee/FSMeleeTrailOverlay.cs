using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._FinalStand.Melee;

public sealed class FSMeleeTrailOverlay(FSMeleeJuiceSystem juice) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly Vector2[] _quad = new Vector2[4];

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var now = juice.RealTime;

        foreach (var trail in juice.Trails)
        {
            if (trail.Map != args.MapId || trail.Points.Count < 2)
                continue;

            for (var i = 1; i < trail.Points.Count; i++)
            {
                var (a, aTime) = trail.Points[i - 1];
                var (b, bTime) = trail.Points[i];
                var life = 1f - (now - bTime) / trail.Duration;
                if (life <= 0f)
                    continue;

                var segment = b - a;
                if (segment.LengthSquared() < 0.0001f)
                    continue;

                var lifeA = Math.Max(0f, 1f - (now - aTime) / trail.Duration);
                var normal = new Vector2(-segment.Y, segment.X).Normalized();
                var widthA = trail.Width * lifeA;
                var widthB = trail.Width * life;

                _quad[0] = a + normal * widthA;
                _quad[1] = b + normal * widthB;
                _quad[2] = b - normal * widthB;
                _quad[3] = a - normal * widthA;
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _quad, trail.Color.WithAlpha(trail.Color.A * life * trail.Opacity));
            }
        }
    }
}
