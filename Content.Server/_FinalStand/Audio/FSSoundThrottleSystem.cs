using Content.Shared.GameTicking;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Audio;

/// <summary>
/// Drops repeats of a burst sound when enough copies already played nearby very recently.
/// </summary>
public sealed partial class FSSoundThrottleSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<string, List<(TimeSpan Time, MapCoordinates Pos)>> _recent = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _recent.Clear());
    }

    public void PlayPvsThrottled(SoundSpecifier? sound, EntityUid source, TimeSpan window, float radius, int maxPlays)
    {
        if (sound == null)
            return;

        string key = sound switch
        {
            SoundPathSpecifier path => path.Path.ToString(),
            SoundCollectionSpecifier collection => collection.Collection?.ToString() ?? string.Empty,
            _ => sound.GetType().Name,
        };
        var pos = _transform.GetMapCoordinates(source);
        var now = _timing.CurTime;

        if (!_recent.TryGetValue(key, out var plays))
        {
            plays = new List<(TimeSpan, MapCoordinates)>();
            _recent[key] = plays;
        }

        plays.RemoveAll(p => now - p.Time > window);

        var nearby = 0;
        foreach (var (_, played) in plays)
        {
            if (played.MapId == pos.MapId && (played.Position - pos.Position).LengthSquared() <= radius * radius)
                nearby++;
        }

        if (nearby >= maxPlays)
            return;

        plays.Add((now, pos));
        _audio.PlayPvs(sound, source);
    }
}
