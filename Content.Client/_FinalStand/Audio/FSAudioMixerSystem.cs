using Content.Shared._FinalStand.Audio;
using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Audio;

/// <summary>
/// Client-side mixing on top of each sound's own volume: per-file trims, the weapons slider,
/// crowd compensation for identical sounds stacking, and a limiter on the combined weapons level.
/// Runs after <see cref="AudioSystem"/>, which resets every stream's volume on its audio tick.
/// </summary>
public sealed partial class FSAudioMixerSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const string TrimsId = "Default";
    private const float CrowdStrength = 0.8f;
    private const float CrowdFloorDb = -12f;
    private const float AttackSeconds = 0.03f;
    private const float ReleaseSeconds = 0.3f;

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

    private readonly Dictionary<string, FileMix?> _mix = new();
    private readonly Dictionary<string, int> _counts = new();
    private readonly List<(AudioComponent Comp, FileMix Mix, float Distance)> _audible = new();
    private Dictionary<string, float> _trims = new();

    private float _weaponDb;
    private bool _dynamics;
    private float _headroom;
    private float _limiterDb;

    private sealed class FileMix
    {
        public float Offset;
        public bool Weapon;
        public bool Crowd;
    }

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(AudioSystem));
        Subs.CVar(_cfg, CCVars.FSWeaponsVolume, SetWeaponsVolume, true);
        Subs.CVar(_cfg, CCVars.FSAudioDynamics, v => _dynamics = v, true);
        Subs.CVar(_cfg, CCVars.FSWeaponsHeadroom, v => _headroom = MathF.Max(v, 0.01f), true);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        LoadTrims();
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
        _mix.Clear();
    }

    private void SetWeaponsVolume(float gain)
    {
        _weaponDb = MathF.Max(SharedAudioSystem.GainToVolume(gain), -80f);
        _mix.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        _audible.Clear();
        _counts.Clear();

        var listener = _audio.GetListenerCoordinates();
        var query = AllEntityQuery<AudioComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            var mix = GetMix(comp.FileName);
            if (mix == null)
                continue;

            // Zero gain means the engine muted it (out of range or another map).
            if (comp.Gain <= 0f)
                continue;

            var distance = 0f;
            if (!comp.Global && xform.MapID == listener.MapId)
                distance = (_transform.GetWorldPosition(xform) - listener.Position).Length();

            _audible.Add((comp, mix, distance));

            if (mix.Crowd)
                _counts[comp.FileName] = _counts.GetValueOrDefault(comp.FileName) + 1;
        }

        var weaponSum = 0f;
        foreach (var (comp, mix, distance) in _audible)
        {
            if (!mix.Weapon || !_dynamics)
                continue;

            var volume = comp.Params.Volume + mix.Offset + CrowdDb(comp.FileName, mix);
            weaponSum += SharedAudioSystem.VolumeToGain(volume) * DistanceFactor(comp, distance);
        }

        UpdateLimiter(weaponSum, frameTime);

        foreach (var (comp, mix, _) in _audible)
        {
            var offset = mix.Offset + CrowdDb(comp.FileName, mix);
            if (mix.Weapon && _dynamics)
                offset += _limiterDb;

            if (offset != 0f)
                comp.Volume = comp.Params.Volume + offset;
        }
    }

    private void UpdateLimiter(float weaponSum, float frameTime)
    {
        var target = weaponSum > _headroom && _dynamics
            ? SharedAudioSystem.GainToVolume(_headroom / weaponSum)
            : 0f;

        var seconds = target < _limiterDb ? AttackSeconds : ReleaseSeconds;
        var blend = 1f - MathF.Exp(-frameTime / seconds);
        _limiterDb += (target - _limiterDb) * blend;
    }

    private float CrowdDb(string fileName, FileMix mix)
    {
        if (!_dynamics || !mix.Crowd)
            return 0f;

        var count = _counts.GetValueOrDefault(fileName);
        if (count < 2)
            return 0f;

        return MathF.Max(-CrowdStrength * 10f * MathF.Log10(count), CrowdFloorDb);
    }

    private float DistanceFactor(AudioComponent comp, float distance)
    {
        var reference = _audio.GetAudioDistance(comp.Params.ReferenceDistance);
        var max = _audio.GetAudioDistance(comp.Params.MaxDistance);
        var d = Math.Clamp(_audio.GetAudioDistance(distance), reference, max);

        if (max <= reference)
            return 1f;

        return Math.Clamp(1f - comp.Params.RolloffFactor * (d - reference) / (max - reference), 0f, 1f);
    }

    private FileMix? GetMix(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;

        if (_mix.TryGetValue(fileName, out var cached))
            return cached;

        var weapon = HasPrefix(fileName, WeaponPrefixes);
        var offset = _trims.GetValueOrDefault(fileName) + (weapon ? _weaponDb : 0f);
        var crowd = HasPrefix(fileName, CrowdPrefixes);

        FileMix? mix = weapon || crowd || offset != 0f
            ? new FileMix { Offset = offset, Weapon = weapon, Crowd = crowd }
            : null;

        _mix[fileName] = mix;
        return mix;
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
