using System.Numerics;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

// Rising chevrons over a freshly buffed crewmate. Mirrors the medigun beam's drifting crosses so the
// two read as the same visual language, pointing up because a buff is a gain.
public sealed class FSBuffFlashOverlay : Overlay
{
    private const int Chevrons = 3;
    private const float RiseHeight = 0.75f;
    private const float BaseOffset = 0.55f;
    private const float ChevronWidth = 0.15f;
    private const float ChevronHeight = 0.09f;
    private const float ChevronGap = 0.08f;
    private const float Thickness = 0.035f;

    private readonly IEntityManager _entities;
    private readonly IGameTiming _timing;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public FSBuffFlashOverlay(IEntityManager entities, IGameTiming timing)
    {
        _entities = entities;
        _timing = timing;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var now = _timing.CurTime;
        var xforms = _entities.GetEntityQuery<TransformComponent>();

        var query = _entities.EntityQueryEnumerator<FSBuffFlashComponent>();

        while (query.MoveNext(out var uid, out var flash))
        {
            if (now >= flash.EndTime || !xforms.TryGetComponent(uid, out var xform))
                continue;

            if (xform.MapID != args.MapId)
                continue;

            var position = _entities.System<SharedTransformSystem>().GetWorldPosition(xform);
            if (!args.WorldBounds.Contains(position))
                continue;

            var remaining = (float) (flash.EndTime - now).TotalSeconds;
            var elapsed = MathF.Max(0f, 2f - remaining);

            for (var i = 0; i < Chevrons; i++)
            {
                var phase = (elapsed * 0.9f + i / (float) Chevrons) % 1f;

                var rise = phase * RiseHeight;
                var fade = MathF.Sin(phase * MathF.PI);
                if (fade <= 0.01f)
                    continue;

                var sway = MathF.Sin((elapsed + i) * 2.6f) * 0.06f;
                var centre = position + new Vector2(sway, BaseOffset + rise);
                var colour = flash.Colour.WithAlpha(fade * 0.95f);

                DrawChevron(handle, centre, colour);
                DrawChevron(handle, centre - new Vector2(0f, ChevronGap), colour.WithAlpha(fade * 0.5f));
            }
        }
    }

    private static void DrawChevron(DrawingHandleWorld handle, Vector2 centre, Color colour)
    {
        var left = centre + new Vector2(-ChevronWidth * 0.5f, -ChevronHeight * 0.5f);
        var apex = centre + new Vector2(0f, ChevronHeight * 0.5f);
        var right = centre + new Vector2(ChevronWidth * 0.5f, -ChevronHeight * 0.5f);

        handle.DrawLine(left, apex, colour);
        handle.DrawLine(apex, right, colour);

        // DrawLine is a hairline, so a second offset pass gives the stroke some weight.
        var lift = new Vector2(0f, Thickness);
        handle.DrawLine(left + lift, apex + lift, colour);
        handle.DrawLine(apex + lift, right + lift, colour);
    }
}
