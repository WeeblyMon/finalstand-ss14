using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;

namespace Content.Client._FinalStand.Audio;

/// <summary>
/// Applies the weapons volume slider on top of each sound's own volume.
/// Runs after <see cref="AudioSystem"/>, which resets every stream's volume on its audio tick.
/// </summary>
public sealed partial class FSAudioMixerSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

    private static readonly string[] WeaponPrefixes =
    {
        "/Audio/Weapons/",
        "/Audio/_FinalStand/Weapons/",
    };

    private readonly Dictionary<string, bool> _isWeapon = new();
    private float _weaponDb;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(AudioSystem));
        Subs.CVar(_cfg, CCVars.FSWeaponsVolume, SetWeaponsVolume, true);
    }

    private void SetWeaponsVolume(float gain)
    {
        _weaponDb = MathF.Max(SharedAudioSystem.GainToVolume(gain), -80f);
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_weaponDb == 0f)
            return;

        var query = AllEntityQuery<AudioComponent>();
        while (query.MoveNext(out var comp))
        {
            if (!IsWeapon(comp.FileName))
                continue;

            // Zero gain means the engine muted it (out of range or another map).
            if (comp.Gain <= 0f)
                continue;

            comp.Volume = comp.Params.Volume + _weaponDb;
        }
    }

    private bool IsWeapon(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;

        if (_isWeapon.TryGetValue(fileName, out var cached))
            return cached;

        var result = false;
        foreach (var prefix in WeaponPrefixes)
        {
            if (fileName.StartsWith(prefix, StringComparison.Ordinal))
            {
                result = true;
                break;
            }
        }

        _isWeapon[fileName] = result;
        return result;
    }
}
