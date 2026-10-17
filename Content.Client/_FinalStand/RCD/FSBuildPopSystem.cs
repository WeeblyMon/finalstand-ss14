using System.Numerics;
using Content.Shared._FinalStand.RCD;
using Content.Shared.CCVar;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.RCD;

public sealed partial class FSBuildPopSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const float Duration = 0.24f;

    private readonly Dictionary<EntityUid, (TimeSpan Start, Vector2 Scale)> _pops = new();
    private readonly List<EntityUid> _done = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSBuildLandedEvent>(OnLanded);
    }

    private void OnLanded(FSBuildLandedEvent ev)
    {
        if (!_cfg.GetCVar(CCVars.FSEngBuildPop)
            || !TryGetEntity(ev.Built, out var built)
            || !TryComp<SpriteComponent>(built, out var sprite))
            return;

        var scale = _pops.TryGetValue(built.Value, out var running) ? running.Scale : sprite.Scale;
        _pops[built.Value] = (_timing.RealTime, scale);
    }

    public override void FrameUpdate(float frameTime)
    {
        foreach (var (uid, pop) in _pops)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite))
            {
                _done.Add(uid);
                continue;
            }

            var t = (float) (_timing.RealTime - pop.Start).TotalSeconds / Duration;
            if (t >= 1f)
            {
                _sprite.SetScale((uid, sprite), pop.Scale);
                _done.Add(uid);
                continue;
            }

            _sprite.SetScale((uid, sprite), pop.Scale * Curve(t));
        }

        foreach (var uid in _done)
            _pops.Remove(uid);
        _done.Clear();
    }

    // Squashed wide on landing, springs a little tall, then settles.
    private static Vector2 Curve(float t)
    {
        if (t < 0.35f)
        {
            var k = t / 0.35f;
            return new Vector2(1.12f - 0.12f * k, 0.82f + 0.26f * k);
        }

        var e = (t - 0.35f) / 0.65f;
        var wobble = MathF.Sin(e * MathF.PI) * (1f - e);
        return new Vector2(1f - 0.03f * wobble, 1.08f - 0.08f * e + 0.02f * wobble);
    }
}
