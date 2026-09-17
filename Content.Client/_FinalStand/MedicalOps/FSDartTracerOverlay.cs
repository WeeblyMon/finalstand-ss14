using System.Numerics;
using Content.Shared._FinalStand.FriendlyFire;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Physics.Components;

namespace Content.Client._FinalStand.MedicalOps;

// Support darts have to be readable at a glance: you are shooting a moving teammate, and a 1px
// syringe crossing a lit room is not. The streak is drawn from the dart back along its own
// velocity, so it needs no position history.
public sealed class FSDartTracerOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly SharedTransformSystem _transform;

    private const float TrailSeconds = 0.08f;
    private const int Segments = 5;

    private static readonly Color Tracer = Color.FromHex("#5FE3B4");

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public FSDartTracerOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
        _transform = _entManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        var query = _entManager.EntityQueryEnumerator<FSAllyProjectileComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var physics, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var velocity = physics.LinearVelocity;
            if (velocity.LengthSquared() < 1f)
                continue;

            var head = _transform.GetWorldPosition(xform);
            if (!args.WorldAABB.Enlarged(2f).Contains(head))
                continue;

            var tail = head - velocity * TrailSeconds;

            // Tapered: brightest at the dart, fading to nothing behind it.
            for (var i = 0; i < Segments; i++)
            {
                var t0 = i / (float) Segments;
                var t1 = (i + 1) / (float) Segments;

                handle.DrawLine(
                    Vector2.Lerp(head, tail, t0),
                    Vector2.Lerp(head, tail, t1),
                    Tracer.WithAlpha(0.85f * (1f - t0)));
            }
        }
    }
}
