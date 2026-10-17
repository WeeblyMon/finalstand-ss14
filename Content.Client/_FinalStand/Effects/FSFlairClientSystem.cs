using System.Numerics;
using Content.Client._FinalStand.Particles;
using Content.Shared._FinalStand.Effects;
using Content.Shared.Camera;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Effects;

public sealed record FSHologram(EntityCoordinates Coords, string Piece, TimeSpan Start, TimeSpan End, Angle Rotation);

public sealed partial class FSFlairClientSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private FSParticleSystem _particles = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float KickRange = 4f;
    private const float KickStrength = 0.35f;

    private readonly List<(TimeSpan At, FSFlairBurstEvent Ev)> _pending = new();
    public readonly List<FSHologram> Holograms = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSFlairBurstEvent>(OnBurst);
        SubscribeNetworkEvent<FSBuildHologramEvent>(OnHologram);
        _overlays.AddOverlay(new FSFlairOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSFlairOverlay>();
    }

    private void OnBurst(FSFlairBurstEvent ev)
    {
        if (ev.Delay <= 0f)
        {
            Play(ev);
            return;
        }

        _pending.Add((_timing.RealTime + TimeSpan.FromSeconds(ev.Delay), ev));
    }

    private void OnHologram(FSBuildHologramEvent ev)
    {
        var now = _timing.RealTime;
        Holograms.Add(new FSHologram(GetCoordinates(ev.Coords), ev.Piece, now, now + TimeSpan.FromSeconds(ev.Duration), ev.Rotation));
    }

    private void Play(FSFlairBurstEvent ev)
    {
        var coords = GetCoordinates(ev.Coords);
        if (!coords.IsValid(EntityManager))
            return;

        var map = _transform.ToMapCoordinates(coords);
        _particles.Burst(ev.Emitter, map, ev.Tint, ev.Count);

        if (ev.Kick > 0f)
            Kick(map, KickStrength * ev.Kick, KickRange);
    }

    public void Kick(MapCoordinates at, float strength, float range)
    {
        if (_player.LocalEntity is not { } local)
            return;

        var me = _transform.GetMapCoordinates(local);
        if (me.MapId != at.MapId || (me.Position - at.Position).Length() > range)
            return;

        var kick = new Vector2(_random.NextFloat(-1f, 1f), _random.NextFloat(-1f, 1f)) * strength;
        _recoil.KickCamera(local, kick);
    }

    public override void FrameUpdate(float frameTime)
    {
        var now = _timing.RealTime;
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            if (now < _pending[i].At)
                continue;

            Play(_pending[i].Ev);
            _pending.RemoveAt(i);
        }

        Holograms.RemoveAll(h => now >= h.End);
    }
}
