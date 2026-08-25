using System.Numerics;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client._FinalStand.Mobs;

public sealed partial class FSRevenantHeartbeatSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private FSRevenantTrackerSystem _tracker = default!;

    private const string HeartbeatSound = "/Audio/_FinalStand/Mobs/Revenant/heartbeat.ogg";

    private const float AudibleRange = 14f;
    private const float PanicRange = 2f;

    private const float SlowestInterval = 2.4f;
    private const float FastestInterval = 1.0f;

    private const float FarVolume = -14f;
    private const float NearVolume = -2f;

    private float _accum;
    private float _nextBeat = SlowestInterval;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalSession?.AttachedEntity is not { } local)
            return;

        if (!_tracker.TryGetNearest(out var nearest, out var distance) || distance > AudibleRange)
        {
            _accum = 0f;
            _nextBeat = SlowestInterval;
            return;
        }

        _accum += frameTime;
        if (_accum < _nextBeat)
            return;

        _accum = 0f;

        var closeness = 1f - Math.Clamp((distance - PanicRange) / (AudibleRange - PanicRange), 0f, 1f);
        var eased = closeness * closeness;

        _nextBeat = float.Lerp(SlowestInterval, FastestInterval, eased);

        _audio.PlayEntity(HeartbeatSound, local, nearest,
            AudioParams.Default.WithVolume(float.Lerp(FarVolume, NearVolume, eased)));
    }
}
