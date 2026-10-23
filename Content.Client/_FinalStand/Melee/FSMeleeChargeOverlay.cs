using System.Numerics;
using Content.Shared._FinalStand.Melee;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Melee;

// Sparks drawn in toward a winding-up weapon while a glow builds around its holder.
public sealed partial class FSMeleeChargeOverlay : Overlay
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const int MaxSparks = 14;
    private const float OuterRadius = 1.3f;
    private const float InnerRadius = 0.2f;
    private const float SparkRate = 1.6f;

    private static readonly Color SparkColor = Color.FromHex("#ffe7a8");
    private static readonly Color GlowColor = Color.FromHex("#fff1c9");

    private SharedTransformSystem? _xform;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public FSMeleeChargeOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_cfg.GetCVar(CCVars.FSMeleeEffects))
            return;

        _xform ??= _entities.System<SharedTransformSystem>();
        var handle = args.WorldHandle;
        var now = _timing.CurTime;
        var time = (float) _timing.RealTime.TotalSeconds;

        var query = _entities.EntityQueryEnumerator<FSMeleeChargeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var charge, out var xform))
        {
            if (charge.ChargeStart == null || xform.MapID != args.MapId)
                continue;

            var centre = _xform.GetWorldPosition(xform);
            if (!args.WorldBounds.Enlarged(OuterRadius).Contains(centre))
                continue;

            var level = charge.ChargeAt(now);
            var pulse = level >= 1f ? 0.5f + 0.5f * MathF.Sin(time * 14f) : 0f;
            handle.DrawCircle(centre, 0.3f + 0.25f * level + 0.06f * pulse, GlowColor.WithAlpha(0.06f + 0.1f * level + 0.08f * pulse));
            handle.DrawCircle(centre, 0.15f + 0.12f * level, GlowColor.WithAlpha(0.08f + 0.12f * level));

            var sparks = (int) MathF.Ceiling(MaxSparks * level);
            var seed = (int) uid.Id;
            for (var i = 0; i < sparks; i++)
            {
                var cycle = time * SparkRate + i / (float) MaxSparks;
                var phase = cycle - MathF.Floor(cycle);
                var angle = Hash(seed + i * 31 + (int) MathF.Floor(cycle) * 977) * MathF.Tau;
                var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var radius = OuterRadius + (InnerRadius - OuterRadius) * phase * phase;
                var tail = 0.12f + 0.2f * phase;
                var from = centre + dir * (radius + tail);
                var to = centre + dir * radius;
                var colour = SparkColor.WithAlpha(MathF.Min(1f, phase * 2.5f) * (0.5f + 0.5f * level));
                var side = new Vector2(-dir.Y, dir.X) * 0.03f;
                handle.DrawLine(from, to, colour);
                handle.DrawLine(from + side, to + side * 0.5f, colour);
                handle.DrawLine(from - side, to - side * 0.5f, colour);
            }
        }
    }

    private static float Hash(int n)
    {
        n = (n << 13) ^ n;
        return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / (float) 0x7fffffff;
    }
}
