using System.Numerics;
using Content.Shared._FinalStand.Audio;
using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Audio;

/// <summary>
/// Client-side mixing on top of each sound's own volume: per-file trims, the weapons slider,
/// compensation when several emitters start the same sound together, and a limiter on the
/// combined weapons level. Offsets are fixed when a sound starts so nothing shifts mid-playback,
/// and they are applied inside the engine's stream update so the source never plays unadjusted.
/// </summary>
public sealed partial class FSAudioMixerSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const string TrimsId = "Default";
    private const float CrowdStrength = 0.8f;
    private const float CrowdFloorDb = -9f;
    private const float AttackSeconds = 0.05f;
    private const float ReleaseSeconds = 0.5f;
    private static readonly TimeSpan CrowdWindow = TimeSpan.FromMilliseconds(80);

    private static readonly string[] WeaponPrefixes =
    {
        "/Audio/Weapons/",
        "/Audio/_FinalStand/Weapons/",
    };

    private static readonly string[] CrowdPrefixes =
    {
        "/Audio/Weapons/",
        "/Audio/_FinalStand/Weapons/",
        "/Audio/_FinalStand/Mobs/",
    };

    private readonly Dictionary<EntityUid, Track> _tracks = new();
    private readonly Dictionary<string, List<(TimeSpan Time, EntityUid Emitter)>> _recentStarts = new();
    private readonly HashSet<EntityUid> _seen = new();
    private readonly List<EntityUid> _stale = new();
    private readonly HashSet<EntityUid> _emitters = new();
    private Dictionary<string, float> _trims = new();
    private EntityQuery<PhysicsComponent> _physicsQuery;

    private float _weaponDb;
    private bool _dynamics;
    private float _headroom;
    private float _limiterDb;

    private sealed class Track
    {
        public float FixedDb;
        public bool Weapon;
    }

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(AudioSystem));
        _physicsQuery = GetEntityQuery<PhysicsComponent>();

        Subs.CVar(_cfg, CCVars.FSWeaponsVolume, v => _weaponDb = MathF.Max(SharedAudioSystem.GainToVolume(v), -80f), true);
        Subs.CVar(_cfg, CCVars.FSAudioDynamics, v => _dynamics = v, true);
        Subs.CVar(_cfg, CCVars.FSWeaponsHeadroom, v => _headroom = MathF.Max(v, 0.01f), true);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        LoadTrims();

        _audio.ProcessStreamOverride += ProcessStream;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _audio.ProcessStreamOverride -= ProcessStream;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<FSSoundTrimPrototype>())
            LoadTrims();
    }

    private void LoadTrims()
    {
        _trims = _proto.TryIndex<FSSoundTrimPrototype>(TrimsId, out var proto)
            ? proto.Trims
            : new Dictionary<string, float>();
    }

    public override void FrameUpdate(float frameTime)
    {
        var now = _timing.RealTime;
        var listener = _audio.GetListenerCoordinates();
        var weaponSum = 0f;
        _seen.Clear();

        var query = AllEntityQuery<AudioComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            _seen.Add(uid);

            if (!_tracks.TryGetValue(uid, out var track))
            {
                track = StartTrack(comp.FileName, xform.ParentUid, now, comp.Started);
                _tracks[uid] = track;
            }

            if (track.Weapon && _dynamics && comp.Started && comp.Playing)
                weaponSum += SharedAudioSystem.VolumeToGain(comp.Params.Volume + track.FixedDb)
                             * DistanceFactor(comp, xform, listener);
        }

        foreach (var uid in _tracks.Keys)
        {
            if (!_seen.Contains(uid))
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
        {
            _tracks.Remove(uid);
        }

        _stale.Clear();
        UpdateLimiter(weaponSum, frameTime);
    }

    private Track StartTrack(string fileName, EntityUid emitter, TimeSpan now, bool alreadyStarted)
    {
        var track = new Track();
        if (string.IsNullOrEmpty(fileName))
            return track;

        track.Weapon = HasPrefix(fileName, WeaponPrefixes);
        track.FixedDb = _trims.GetValueOrDefault(fileName);

        if (!_dynamics || !HasPrefix(fileName, CrowdPrefixes))
            return track;

        var crowdDb = CrowdDb(fileName, emitter, now);

        // Sounds the engine already started (local predicted plays) keep the volume they started with.
        if (alreadyStarted)
            return track;

        track.FixedDb += crowdDb;
        if (track.Weapon)
            track.FixedDb += _limiterDb;

        return track;
    }

    private float CrowdDb(string fileName, EntityUid emitter, TimeSpan now)
    {
        if (!_recentStarts.TryGetValue(fileName, out var starts))
        {
            starts = new List<(TimeSpan, EntityUid)>();
            _recentStarts[fileName] = starts;
        }

        starts.RemoveAll(s => now - s.Time > CrowdWindow);
        starts.Add((now, emitter));

        _emitters.Clear();
        foreach (var (_, other) in starts)
        {
            _emitters.Add(other);
        }

        // Only other emitters count, so one gun firing on its own never ducks itself.
        var count = _emitters.Count;
        if (count < 2)
            return 0f;

        return MathF.Max(-CrowdStrength * 10f * MathF.Log10(count), CrowdFloorDb);
    }

    private void UpdateLimiter(float weaponSum, float frameTime)
    {
        var target = _dynamics && weaponSum > _headroom
            ? SharedAudioSystem.GainToVolume(_headroom / weaponSum)
            : 0f;

        var seconds = target < _limiterDb ? AttackSeconds : ReleaseSeconds;
        _limiterDb += (target - _limiterDb) * (1f - MathF.Exp(-frameTime / seconds));
    }

    private float DistanceFactor(AudioComponent comp, TransformComponent xform, MapCoordinates listener)
    {
        if (comp.Global)
            return 1f;

        if (xform.MapID != listener.MapId)
            return 0f;

        var distance = (_transform.GetWorldPosition(xform) - listener.Position).Length();
        var reference = _audio.GetAudioDistance(comp.Params.ReferenceDistance);
        var max = _audio.GetAudioDistance(comp.Params.MaxDistance);
        if (max <= reference)
            return 1f;

        var d = Math.Clamp(_audio.GetAudioDistance(distance), reference, max);
        return Math.Clamp(1f - comp.Params.RolloffFactor * (d - reference) / (max - reference), 0f, 1f);
    }

    private float TargetVolume(EntityUid entity, AudioComponent component)
    {
        // Read-only here: this runs on the audio job's worker threads.
        if (_tracks.TryGetValue(entity, out var track))
            return component.Params.Volume + track.FixedDb + (track.Weapon ? _weaponDb : 0f);

        var fileName = component.FileName;
        if (string.IsNullOrEmpty(fileName))
            return component.Params.Volume;

        var volume = component.Params.Volume + _trims.GetValueOrDefault(fileName);
        if (HasPrefix(fileName, WeaponPrefixes))
            volume += _weaponDb;

        return volume;
    }

    // Port of Robust.Client.Audio.AudioSystem.ProcessStream with the volume taken from TargetVolume.
    private void ProcessStream(EntityUid entity, AudioComponent component, TransformComponent xform, MapCoordinates listener)
    {
        if (!component.Started)
        {
            component.Started = true;
            component.StartPlaying();
        }

        if (component.Global)
        {
            if (xform.MapID != MapId.Nullspace && listener.MapId != xform.MapID)
            {
                component.Gain = 0f;
                return;
            }

            component.Volume = TargetVolume(entity, component);
            return;
        }

        if (listener.MapId != xform.MapID)
        {
            component.Gain = 0f;
            return;
        }

        var parentUid = xform.ParentUid;
        Vector2 worldPos;
        component.Volume = TargetVolume(entity, component);

        if ((component.Flags & AudioFlags.GridAudio) != 0x0)
            worldPos = _maps.GetGridPosition(parentUid);
        else
            worldPos = _transform.GetWorldPosition(entity);

        var delta = worldPos - listener.Position;
        var distance = delta.Length();

        if (_audio.GetAudioDistance(distance) > component.MaxDistance)
        {
            component.Gain = 0f;
            return;
        }

        if (distance > 0f && distance < 0.01f)
        {
            worldPos = listener.Position;
            delta = Vector2.Zero;
            distance = 0f;
        }

        if ((component.Flags & AudioFlags.NoOcclusion) == AudioFlags.NoOcclusion)
            component.Occlusion = 0f;
        else
            component.Occlusion = _audio.GetOcclusion(listener, delta, distance, parentUid);

        component.Position = worldPos;

        if (_physicsQuery.TryGetComponent(parentUid, out var physicsComp))
            component.Velocity = _physics.GetMapLinearVelocity(parentUid, physicsComp);
    }

    private static bool HasPrefix(string fileName, string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (fileName.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
