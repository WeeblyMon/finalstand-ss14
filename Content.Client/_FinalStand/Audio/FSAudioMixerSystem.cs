using Content.Shared._FinalStand.Audio;
using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Audio;

/// <summary>
/// Applies per-file trims and the weapons volume slider on top of each sound's own volume.
/// Runs after <see cref="AudioSystem"/>, which resets every stream's volume on its audio tick.
/// </summary>
public sealed partial class FSAudioMixerSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private const string TrimsId = "Default";

    private static readonly string[] WeaponPrefixes =
    {
        "/Audio/Weapons/",
        "/Audio/_FinalStand/Weapons/",
    };

    private readonly Dictionary<string, float> _offsets = new();
    private Dictionary<string, float> _trims = new();
    private float _weaponDb;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(AudioSystem));
        Subs.CVar(_cfg, CCVars.FSWeaponsVolume, SetWeaponsVolume, true);
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
        _offsets.Clear();
    }

    private void SetWeaponsVolume(float gain)
    {
        _weaponDb = MathF.Max(SharedAudioSystem.GainToVolume(gain), -80f);
        _offsets.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = AllEntityQuery<AudioComponent>();
        while (query.MoveNext(out var comp))
        {
            var offset = GetOffset(comp.FileName);
            if (offset == 0f)
                continue;

            // Zero gain means the engine muted it (out of range or another map).
            if (comp.Gain <= 0f)
                continue;

            comp.Volume = comp.Params.Volume + offset;
        }
    }

    private float GetOffset(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return 0f;

        if (_offsets.TryGetValue(fileName, out var cached))
            return cached;

        var offset = _trims.GetValueOrDefault(fileName);
        if (IsWeapon(fileName))
            offset += _weaponDb;

        _offsets[fileName] = offset;
        return offset;
    }

    private static bool IsWeapon(string fileName)
    {
        foreach (var prefix in WeaponPrefixes)
        {
            if (fileName.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
